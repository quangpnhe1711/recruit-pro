using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Request.Discovery;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Services;

public class SemanticDiscoveryService : ISemanticDiscoveryService
{
    private readonly ICandidateProfileRepository _candidateProfileRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IEmbeddingCache _embeddingCache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SemanticDiscoveryService> _logger;

    public SemanticDiscoveryService(
        ICandidateProfileRepository candidateProfileRepository,
        IJobRepository jobRepository,
        IEmbeddingProvider embeddingProvider,
        IEmbeddingCache embeddingCache,
        IUnitOfWork unitOfWork,
        ILogger<SemanticDiscoveryService> logger)
    {
        _candidateProfileRepository = candidateProfileRepository;
        _jobRepository = jobRepository;
        _embeddingProvider = embeddingProvider;
        _embeddingCache = embeddingCache;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<SemanticCandidatesResponseDto>> SearchTalentPoolAsync(TalentPoolSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query) && string.IsNullOrWhiteSpace(request.JobId))
        {
            return ApiResponse<SemanticCandidatesResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        string queryText = request.Query;
        if (string.IsNullOrWhiteSpace(queryText) && !string.IsNullOrWhiteSpace(request.JobId))
        {
            Job job = await GetJobAsync(request.JobId);
            queryText = BuildJobEmbeddingText(job);
        }

        IReadOnlyList<double> queryVector = await BuildTransientEmbeddingAsync(queryText);
        IReadOnlyList<CandidateProfile> candidates = await _candidateProfileRepository.GetAllForSemanticSearchAsync();

        List<SemanticCandidateCardDto> ranked = [];
        foreach (CandidateProfile candidate in candidates)
        {
            IReadOnlyList<double>? candidateVector = await EnsureCandidateVectorAsync(candidate.Id);
            if (candidateVector == null)
            {
                continue;
            }

            decimal similarityScore = NormalizeSimilarityScore(CalculateCosineSimilarity(queryVector, candidateVector));
            if (request.MinimumScore.HasValue && similarityScore < request.MinimumScore.Value)
            {
                continue;
            }

            ranked.Add(MapSemanticCandidate(candidate, similarityScore, request.Query));
        }

        return ApiResponse<SemanticCandidatesResponseDto>.Ok(BuildCandidatesPage(ranked, request.Page, request.PageSize));
    }

    public async Task<ApiResponse<SemanticCandidatesResponseDto>> DiscoverCandidatesAsync(CandidateDiscoveryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return ApiResponse<SemanticCandidatesResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        IReadOnlyList<double> queryVector = await BuildTransientEmbeddingAsync(request.Query);
        IReadOnlyList<CandidateProfile> candidates = await _candidateProfileRepository.GetAllForSemanticSearchAsync();

        List<SemanticCandidateCardDto> ranked = [];
        foreach (CandidateProfile candidate in candidates)
        {
            IReadOnlyList<double>? candidateVector = await EnsureCandidateVectorAsync(candidate.Id);
            if (candidateVector == null)
            {
                continue;
            }

            decimal similarityScore = NormalizeSimilarityScore(CalculateCosineSimilarity(queryVector, candidateVector));
            if (request.MinimumScore.HasValue && similarityScore < request.MinimumScore.Value)
            {
                continue;
            }

            ranked.Add(MapSemanticCandidate(candidate, similarityScore, request.Query));
        }

        return ApiResponse<SemanticCandidatesResponseDto>.Ok(BuildCandidatesPage(ranked, request.Page, request.PageSize));
    }

    public async Task<ApiResponse<SemanticCandidatesResponseDto>> GetSimilarCandidatesAsync(string candidateId, int page, int pageSize)
    {
        if (!Guid.TryParse(candidateId, out Guid candidateGuid))
        {
            return ApiResponse<SemanticCandidatesResponseDto>.NotFound(ErrorCodes.CandidateNotFound);
        }

        CandidateProfile target = await GetCandidateAsync(candidateGuid);
        IReadOnlyList<double>? targetVector = await EnsureCandidateVectorAsync(target.Id);
        if (targetVector == null)
        {
            return ApiResponse<SemanticCandidatesResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        IReadOnlyList<CandidateProfile> candidates = await _candidateProfileRepository.GetAllForSemanticSearchAsync();
        List<SemanticCandidateCardDto> ranked = [];
        foreach (CandidateProfile candidate in candidates.Where(candidate => candidate.Id != target.Id))
        {
            IReadOnlyList<double>? candidateVector = await EnsureCandidateVectorAsync(candidate.Id);
            if (candidateVector == null)
            {
                continue;
            }

            decimal similarityScore = NormalizeSimilarityScore(CalculateCosineSimilarity(targetVector, candidateVector));
            ranked.Add(MapSemanticCandidate(candidate, similarityScore, $"Similar to {target.User.FullName}"));
        }

        return ApiResponse<SemanticCandidatesResponseDto>.Ok(BuildCandidatesPage(ranked, page, pageSize));
    }

    public async Task<ApiResponse<SemanticJobsResponseDto>> GetSimilarJobsAsync(string jobId, int page, int pageSize)
    {
        Job target = await GetJobAsync(jobId);
        IReadOnlyList<double>? targetVector = await EnsureJobVectorAsync(target.Id);
        if (targetVector == null)
        {
            return ApiResponse<SemanticJobsResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        IReadOnlyList<Job> jobs = await _jobRepository.GetAllApprovedForSemanticSearchAsync();
        List<SemanticJobCardDto> ranked = [];
        foreach (Job job in jobs.Where(job => job.Id != target.Id))
        {
            IReadOnlyList<double>? jobVector = await EnsureJobVectorAsync(job.Id);
            if (jobVector == null)
            {
                continue;
            }

            decimal similarityScore = NormalizeSimilarityScore(CalculateCosineSimilarity(targetVector, jobVector));
            ranked.Add(MapSemanticJob(job, similarityScore));
        }

        return ApiResponse<SemanticJobsResponseDto>.Ok(BuildJobsPage(ranked, page, pageSize));
    }

    public async Task<ApiResponse<SemanticCandidatesResponseDto>> GetRecommendedCandidatesAsync(string jobId, Guid? callerUserId, IReadOnlyCollection<string> callerRoles, int page, int pageSize)
    {
        Job job = await GetJobAsync(jobId);
        // Phase 2.2b: job-level ownership check — caller must own the job to access its candidate ranking.
        if (!OwnershipScope.CanAccessJob(job, callerUserId, callerRoles))
        {
            return ApiResponse<SemanticCandidatesResponseDto>.Forbidden(ErrorCodes.Forbidden);
        }

        IReadOnlyList<double>? jobVector = await EnsureJobVectorAsync(job.Id);
        if (jobVector == null)
        {
            return ApiResponse<SemanticCandidatesResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        IReadOnlyList<CandidateProfile> candidates = await _candidateProfileRepository.GetAllForSemanticSearchAsync();
        List<SemanticCandidateCardDto> ranked = [];
        foreach (CandidateProfile candidate in candidates)
        {
            IReadOnlyList<double>? candidateVector = await EnsureCandidateVectorAsync(candidate.Id);
            if (candidateVector == null)
            {
                continue;
            }

            decimal similarityScore = NormalizeSimilarityScore(CalculateCosineSimilarity(jobVector, candidateVector));
            ranked.Add(MapSemanticCandidate(candidate, similarityScore, $"Recommended for {job.Title}"));
        }

        return ApiResponse<SemanticCandidatesResponseDto>.Ok(BuildCandidatesPage(ranked, page, pageSize));
    }

    public async Task<ApiResponse<IReadOnlyList<RecommendedJobDto>>> GetRecommendedJobsForCandidateAsync(Guid userId, int take)
    {
        CandidateProfile? profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        if (profile == null)
        {
            throw new BusinessAppException(ErrorCodes.CandidateProfileNotFound, 404);
        }

        IReadOnlyList<double>? candidateVector = await EnsureCandidateVectorAsync(profile.Id);
        if (candidateVector == null)
        {
            return ApiResponse<IReadOnlyList<RecommendedJobDto>>.Ok([]);
        }

        IReadOnlyList<Job> jobs = await _jobRepository.GetAllApprovedForSemanticSearchAsync();
        List<RecommendedJobDto> ranked = [];
        foreach (Job job in jobs)
        {
            IReadOnlyList<double>? jobVector = await EnsureJobVectorAsync(job.Id);
            if (jobVector == null)
            {
                continue;
            }

            decimal similarityScore = NormalizeSimilarityScore(CalculateCosineSimilarity(candidateVector, jobVector));
            ranked.Add(new RecommendedJobDto
            {
                Id = job.Id.ToString(),
                Title = job.Title,
                Meta = $"{job.WorkMode} • Score {similarityScore:0.#}",
                EmploymentType = job.EmploymentType.ToString(),
                Skills = job.JobSkills.Select(jobSkill => jobSkill.Skill.Name).Distinct().Take(6).ToList()
            });
        }

        return ApiResponse<IReadOnlyList<RecommendedJobDto>>.Ok(
            ranked.OrderByDescending(item => ExtractScoreFromMeta(item.Meta)).Take(Math.Max(take, 1)).ToList());
    }

    public async Task RefreshCandidateEmbeddingAsync(Guid candidateProfileId)
    {
        _ = await EnsureCandidateVectorAsync(candidateProfileId);
    }

    public async Task RefreshJobEmbeddingAsync(Guid jobId)
    {
        _ = await EnsureJobVectorAsync(jobId);
    }

    private async Task<IReadOnlyList<double>> BuildTransientEmbeddingAsync(string text)
    {
        EmbeddingGenerationResult result = await _embeddingProvider.GenerateEmbeddingAsync(text);
        if (!result.Succeeded || result.Vector.Count == 0)
        {
            throw new InvalidOperationException(result.FailureReason ?? "Tạo embedding thất bại.");
        }

        return result.Vector;
    }

    private async Task<IReadOnlyList<double>?> EnsureCandidateVectorAsync(Guid candidateId)
    {
        CandidateProfile? candidate = await _candidateProfileRepository.GetTrackedByIdAsync(candidateId);
        if (candidate == null)
        {
            return null;
        }

        string text = BuildCandidateEmbeddingText(candidate);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string hash = ComputeSha256(text);
        if (string.Equals(candidate.CandidateEmbeddingTextHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<double>? storedVector = TryResolveStoredVector(candidate.CandidateEmbeddingVectorJson, hash);
            if (storedVector != null)
            {
                return storedVector;
            }
        }

        EmbeddingGenerationResult result = await _embeddingProvider.GenerateEmbeddingAsync(text);
        if (!result.Succeeded || result.Vector.Count == 0)
        {
            _logger.LogWarning("Candidate embedding refresh failed for candidate {CandidateId}: {Reason}", candidateId, result.FailureReason);
            return null;
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            candidate.CandidateEmbeddingVectorJson = JsonSerializer.Serialize(result.Vector);
            candidate.CandidateEmbeddingTextHash = hash;
            candidate.CandidateEmbeddingStatus = "Completed";
            candidate.CandidateEmbeddingError = null;
            candidate.CandidateEmbeddingUpdatedAt = DbDateTime.Now;
            await _candidateProfileRepository.UpdateAsync(candidate);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            _embeddingCache.Set(hash, result.Vector);
            return result.Vector;
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    private async Task<IReadOnlyList<double>?> EnsureJobVectorAsync(Guid jobId)
    {
        Job? job = await _jobRepository.GetTrackedByIdAsync(jobId);
        if (job == null)
        {
            return null;
        }

        string text = BuildJobEmbeddingText(job);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string hash = ComputeSha256(text);
        if (string.Equals(job.JobEmbeddingTextHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<double>? storedVector = TryResolveStoredVector(job.JobEmbeddingVectorJson, hash);
            if (storedVector != null)
            {
                return storedVector;
            }
        }

        EmbeddingGenerationResult result = await _embeddingProvider.GenerateEmbeddingAsync(text);
        if (!result.Succeeded || result.Vector.Count == 0)
        {
            _logger.LogWarning("Job embedding refresh failed for job {JobId}: {Reason}", jobId, result.FailureReason);
            return null;
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            job.JobEmbeddingVectorJson = JsonSerializer.Serialize(result.Vector);
            job.JobEmbeddingTextHash = hash;
            job.JobEmbeddingStatus = "Completed";
            job.JobEmbeddingError = null;
            job.JobEmbeddingUpdatedAt = DbDateTime.Now;
            await _jobRepository.UpdateAsync(job);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            _embeddingCache.Set(hash, result.Vector);
            return result.Vector;
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    private async Task<CandidateProfile> GetCandidateAsync(Guid candidateId)
    {
        CandidateProfile? candidate = await _candidateProfileRepository.GetByIdAsync(candidateId);
        if (candidate == null)
        {
            throw new BusinessAppException(ErrorCodes.CandidateNotFound, 404);
        }

        return candidate;
    }

    private async Task<Job> GetJobAsync(string jobId)
    {
        if (!Guid.TryParse(jobId, out Guid jobGuid))
        {
            throw new BusinessAppException(ErrorCodes.JobNotFound, 404);
        }

        Job? job = await _jobRepository.GetByIdAsync(jobGuid);
        if (job == null)
        {
            throw new BusinessAppException(ErrorCodes.JobNotFound, 404);
        }

        return job;
    }

    private static SemanticCandidatesResponseDto BuildCandidatesPage(List<SemanticCandidateCardDto> ranked, int page, int pageSize)
    {
        int safePage = Math.Max(page, 1);
        int safePageSize = Math.Max(pageSize, 1);
        List<SemanticCandidateCardDto> items = ranked
            .OrderByDescending(item => item.SimilarityScore)
            .ThenBy(item => item.FullName)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        return new SemanticCandidatesResponseDto
        {
            Items = items,
            Meta = PaginationMetaBuilder.Build(safePage, safePageSize, ranked.Count)
        };
    }

    private static SemanticJobsResponseDto BuildJobsPage(List<SemanticJobCardDto> ranked, int page, int pageSize)
    {
        int safePage = Math.Max(page, 1);
        int safePageSize = Math.Max(pageSize, 1);
        List<SemanticJobCardDto> items = ranked
            .OrderByDescending(item => item.SimilarityScore)
            .ThenBy(item => item.Title)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        return new SemanticJobsResponseDto
        {
            Items = items,
            Meta = PaginationMetaBuilder.Build(safePage, safePageSize, ranked.Count)
        };
    }

    private static SemanticCandidateCardDto MapSemanticCandidate(CandidateProfile candidate, decimal similarityScore, string query)
    {
        List<string> topSkills = candidate.CandidateSkills
            .Where(detail => detail.Skill != null)
            .Select(detail => detail.Skill.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        return new SemanticCandidateCardDto
        {
            CandidateId = candidate.Id.ToString(),
            UserId = candidate.UserId.ToString(),
            FullName = candidate.User.FullName,
            Email = candidate.User.Email,
            AvatarUrl = candidate.User.AvatarUrl,
            Headline = candidate.CurrentPosition,
            Location = candidate.Address,
            ExperienceYears = candidate.ExperienceYears,
            SimilarityScore = similarityScore,
            TopSkills = topSkills,
            SummarySnippet = BuildSummarySnippet(candidate.Bio, candidate.CurrentPosition, topSkills),
            MatchReason = BuildCandidateMatchReason(candidate, query, topSkills)
        };
    }

    private static SemanticJobCardDto MapSemanticJob(Job job, decimal similarityScore)
    {
        List<string> topSkills = job.JobSkills.Select(jobSkill => jobSkill.Skill.Name).Distinct().Take(6).ToList();
        return new SemanticJobCardDto
        {
            JobId = job.Id.ToString(),
            Title = job.Title,
            Department = job.Department?.Name ?? string.Empty,
            Location = job.Location,
            WorkMode = job.WorkMode.ToString(),
            EmploymentType = job.EmploymentType.ToString(),
            SimilarityScore = similarityScore,
            TopSkills = topSkills,
            SummarySnippet = BuildSummarySnippet(job.ShortPitch, job.Description, topSkills)
        };
    }

    private static string BuildCandidateEmbeddingText(CandidateProfile profile)
    {
        StringBuilder builder = new();
        builder.AppendLine("Candidate Profile");
        AppendSection(builder, "Headline", profile.CurrentPosition);
        AppendSection(builder, "Summary", profile.Bio);
        AppendSection(builder, "Skills", string.Join(", ", profile.CandidateSkills
            .Where(detail => detail.Skill != null)
            .Select(detail => detail.YearsOfExperience.HasValue ? $"{detail.Skill.Name} ({detail.YearsOfExperience:0.#} years)" : detail.Skill.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)));
        AppendSection(builder, "Experience", FlattenJsonArray(profile.ExperienceEntriesJson, ["title", "company", "bullets"]));
        AppendSection(builder, "Projects", string.Join(Environment.NewLine, profile.Projects.Select(project =>
            $"{project.Name}. Role: {project.Role}. Description: {project.Description}. Technologies: {FlattenJsonArray(project.TechnologiesJson, [])}")));
        AppendSection(builder, "Education", FlattenJsonArray(profile.EducationRecordsJson, ["school", "degree", "fieldOfStudy", "description"]));
        AppendSection(builder, "Certifications", FlattenJsonArray(profile.CertificationRecordsJson, ["name", "issuer"]));
        AppendSection(builder, "Languages", FlattenJsonArray(profile.LanguageRecordsJson, ["name", "proficiency"]));
        AppendSection(builder, "Keywords", ExtractKeywordsFromParsedResume(profile.ParsedResumeJson));
        AppendSection(builder, "Structured Sections", CandidateProfileSectionHelper.BuildStructuredNarrative(profile));
        return builder.ToString().Trim();
    }

    private static string BuildJobEmbeddingText(Job job)
    {
        StringBuilder builder = new();
        builder.AppendLine("Job Profile");
        AppendSection(builder, "Title", job.Title);
        AppendSection(builder, "Short Pitch", job.ShortPitch);
        AppendSection(builder, "Description", string.Join(Environment.NewLine, ParseJsonArray(job.Description)));
        AppendSection(builder, "Requirements", string.Join(Environment.NewLine, ParseJsonArray(job.Requirements)));
        AppendSection(builder, "Required Skills", string.Join(", ", job.JobSkills.Where(skill => skill.IsRequired).Select(skill => skill.Skill.Name)));
        AppendSection(builder, "Nice-To-Have Skills", string.Join(", ", job.JobSkills.Where(skill => !skill.IsRequired).Select(skill => skill.Skill.Name)));
        AppendSection(builder, "Minimum Experience", job.MinExperienceYears.HasValue ? $"{job.MinExperienceYears} years" : null);
        AppendSection(builder, "Work Mode", job.WorkMode.ToString());
        AppendSection(builder, "Employment Type", job.EmploymentType.ToString());
        AppendSection(builder, "Location", job.Location);
        return builder.ToString().Trim();
    }

    private IReadOnlyList<double>? TryResolveStoredVector(string? vectorJson, string hash)
    {
        if (_embeddingCache.TryGet(hash, out IReadOnlyList<double>? cached) && cached != null)
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(vectorJson))
        {
            return null;
        }

        try
        {
            List<double>? vector = JsonSerializer.Deserialize<List<double>>(vectorJson);
            if (vector == null)
            {
                return null;
            }

            _embeddingCache.Set(hash, vector);
            return vector;
        }
        catch
        {
            return null;
        }
    }

    private static decimal NormalizeSimilarityScore(double similarity)
    {
        return Math.Round((decimal)Math.Clamp(similarity * 100d, 0d, 100d), 2, MidpointRounding.AwayFromZero);
    }

    private static double CalculateCosineSimilarity(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        if (left.Count == 0 || right.Count == 0 || left.Count != right.Count)
        {
            return 0;
        }

        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;
        for (int index = 0; index < left.Count; index += 1)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude <= 0 || rightMagnitude <= 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private static string ComputeSha256(string input)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    private static void AppendSection(StringBuilder builder, string title, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        builder.AppendLine(title + ":");
        builder.AppendLine(content.Trim());
        builder.AppendLine();
    }

    private static string FlattenJsonArray(string? json, IReadOnlyList<string> prioritizedProperties)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                List<string> items = [];
                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        string? value = element.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            items.Add(value);
                        }

                        continue;
                    }

                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    List<string> parts = [];
                    foreach (string propertyName in prioritizedProperties)
                    {
                        if (element.TryGetProperty(propertyName, out JsonElement propertyValue))
                        {
                            string? serialized = FlattenJsonValue(propertyValue);
                            if (!string.IsNullOrWhiteSpace(serialized))
                            {
                                parts.Add(serialized);
                            }
                        }
                    }

                    if (parts.Count > 0)
                    {
                        items.Add(string.Join(". ", parts));
                    }
                }

                return string.Join(Environment.NewLine, items);
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static string? FlattenJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.Array => string.Join(", ", element.EnumerateArray().Select(FlattenJsonValue).Where(value => !string.IsNullOrWhiteSpace(value))),
            _ => null
        };
    }

    private static string ExtractKeywordsFromParsedResume(string? parsedResumeJson)
    {
        if (string.IsNullOrWhiteSpace(parsedResumeJson))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(parsedResumeJson);
            if (!document.RootElement.TryGetProperty("keywords", out JsonElement keywords) || keywords.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            return string.Join(", ", keywords.EnumerateArray()
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item)));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildSummarySnippet(string? primary, string? secondary, IReadOnlyList<string> skills)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Length > 160 ? primary[..160] + "..." : primary;
        }

        if (!string.IsNullOrWhiteSpace(secondary))
        {
            return secondary.Length > 160 ? secondary[..160] + "..." : secondary;
        }

        return skills.Count == 0 ? string.Empty : $"Key skills: {string.Join(", ", skills)}";
    }

    private static string BuildCandidateMatchReason(CandidateProfile candidate, string query, IReadOnlyList<string> skills)
    {
        if (skills.Count > 0)
        {
            return $"Strong overlap with {string.Join(", ", skills.Take(3))} for query \"{query}\".";
        }

        return $"Semantic profile match for query \"{query}\".";
    }

    private static List<string> ParseJsonArray(string? jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return [];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonString);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return document.RootElement.EnumerateArray()
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .ToList();
            }
        }
        catch
        {
        }

        return [jsonString];
    }

    private static decimal ExtractScoreFromMeta(string meta)
    {
        int scoreIndex = meta.LastIndexOf("Score ", StringComparison.OrdinalIgnoreCase);
        if (scoreIndex < 0)
        {
            return 0;
        }

        string value = meta[(scoreIndex + 6)..].Trim();
        return decimal.TryParse(value, out decimal parsed) ? parsed : 0;
    }
}
