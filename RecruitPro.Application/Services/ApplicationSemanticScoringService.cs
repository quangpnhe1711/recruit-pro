using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RecruitPro.Application.Common;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Services;

public class ApplicationSemanticScoringService : IApplicationSemanticScoringService
{
    private const string CandidateEmbeddingStatusProcessing = "Processing";
    private const string CandidateEmbeddingStatusCompleted = "Completed";
    private const string CandidateEmbeddingStatusFailed = "Failed";
    private const string JobEmbeddingStatusProcessing = "Processing";
    private const string JobEmbeddingStatusCompleted = "Completed";
    private const string JobEmbeddingStatusFailed = "Failed";
    private const string ScoreStatusSemanticCompleted = "SemanticCompleted";
    private const string ScoreStatusSemanticFailed = "SemanticFailed";
    private readonly IApplicationRepository _applicationRepository;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IEmbeddingCache _embeddingCache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApplicationSemanticScoringService> _logger;

    public ApplicationSemanticScoringService(
        IApplicationRepository applicationRepository,
        IEmbeddingProvider embeddingProvider,
        IEmbeddingCache embeddingCache,
        IUnitOfWork unitOfWork,
        ILogger<ApplicationSemanticScoringService> logger)
    {
        _applicationRepository = applicationRepository;
        _embeddingProvider = embeddingProvider;
        _embeddingCache = embeddingCache;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        Domain.Entities.Application? application = await _applicationRepository.GetTrackedByIdAsync(applicationId);
        if (application == null)
        {
            _logger.LogWarning("Semantic scoring skipped because application {ApplicationId} was not found.", applicationId);
            return;
        }

        CandidateProfile? profile = application.User.CandidateProfile;
        if (profile == null)
        {
            await MarkSemanticFailureAsync(application, "Candidate profile is missing.", cancellationToken);
            return;
        }

        Job job = application.Job;
        string candidateText = BuildCandidateEmbeddingText(profile);
        string jobText = BuildJobEmbeddingText(job);
        if (string.IsNullOrWhiteSpace(candidateText) || string.IsNullOrWhiteSpace(jobText))
        {
            await MarkSemanticFailureAsync(application, "Embedding text could not be built from candidate or job data.", cancellationToken);
            return;
        }

        string candidateHash = ComputeSha256(candidateText);
        string jobHash = ComputeSha256(jobText);

        try
        {
            await _unitOfWork.BeginTransactionAsync();

            IReadOnlyList<double> candidateVector = await ResolveCandidateEmbeddingAsync(profile, candidateText, candidateHash, cancellationToken);
            IReadOnlyList<double> jobVector = await ResolveJobEmbeddingAsync(job, jobText, jobHash, cancellationToken);

            double similarity = CalculateCosineSimilarity(candidateVector, jobVector);
            decimal semanticScore = Math.Round((decimal)Math.Clamp(similarity * 100d, 0d, 100d), 2, MidpointRounding.AwayFromZero);
            decimal baseRuleScore = application.RuleScore ?? application.FinalScore ?? 0m;
            decimal finalScore = Math.Round((baseRuleScore * 0.85m) + (semanticScore * 0.15m), 2, MidpointRounding.AwayFromZero);

            application.SemanticScore = semanticScore;
            application.FinalScore = finalScore;
            application.ScoreStatus = ScoreStatusSemanticCompleted;
            application.ScoreError = null;
            application.ScoredAt = DbDateTime.Now;

            await _applicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            _logger.LogInformation(
                "Semantic scoring completed for application {ApplicationId}. RuleScore={RuleScore}, SemanticScore={SemanticScore}, FinalScore={FinalScore}",
                application.Id,
                application.RuleScore,
                application.SemanticScore,
                application.FinalScore);
        }
        catch (Exception exception)
        {
            await _unitOfWork.RollbackAsync();
            _logger.LogError(exception, "Semantic scoring failed for application {ApplicationId}.", applicationId);

            application = await _applicationRepository.GetTrackedByIdAsync(applicationId);
            if (application != null)
            {
                await MarkSemanticFailureAsync(application, exception.Message, cancellationToken);
            }
        }
    }

    private async Task<IReadOnlyList<double>> ResolveCandidateEmbeddingAsync(
        CandidateProfile profile,
        string candidateText,
        string candidateHash,
        CancellationToken cancellationToken)
    {
        if (string.Equals(profile.CandidateEmbeddingTextHash, candidateHash, StringComparison.OrdinalIgnoreCase)
            && TryResolveStoredVector(profile.CandidateEmbeddingVectorJson, candidateHash, out IReadOnlyList<double>? cachedCandidateVector)
            && cachedCandidateVector != null)
        {
            profile.CandidateEmbeddingStatus = CandidateEmbeddingStatusCompleted;
            profile.CandidateEmbeddingError = null;
            return cachedCandidateVector;
        }

        profile.CandidateEmbeddingStatus = CandidateEmbeddingStatusProcessing;
        profile.CandidateEmbeddingError = null;
        profile.CandidateEmbeddingUpdatedAt = DbDateTime.Now;

        EmbeddingGenerationResult result = await _embeddingProvider.GenerateEmbeddingAsync(candidateText, cancellationToken);
        if (!result.Succeeded)
        {
            profile.CandidateEmbeddingStatus = CandidateEmbeddingStatusFailed;
            profile.CandidateEmbeddingError = result.FailureReason;
            throw new InvalidOperationException($"Candidate embedding failed: {result.FailureReason}");
        }

        profile.CandidateEmbeddingTextHash = candidateHash;
        profile.CandidateEmbeddingVectorJson = JsonSerializer.Serialize(result.Vector);
        profile.CandidateEmbeddingStatus = CandidateEmbeddingStatusCompleted;
        profile.CandidateEmbeddingError = null;
        profile.CandidateEmbeddingUpdatedAt = DbDateTime.Now;
        _embeddingCache.Set(candidateHash, result.Vector);
        return result.Vector;
    }

    private async Task<IReadOnlyList<double>> ResolveJobEmbeddingAsync(
        Job job,
        string jobText,
        string jobHash,
        CancellationToken cancellationToken)
    {
        if (string.Equals(job.JobEmbeddingTextHash, jobHash, StringComparison.OrdinalIgnoreCase)
            && TryResolveStoredVector(job.JobEmbeddingVectorJson, jobHash, out IReadOnlyList<double>? cachedJobVector)
            && cachedJobVector != null)
        {
            job.JobEmbeddingStatus = JobEmbeddingStatusCompleted;
            job.JobEmbeddingError = null;
            return cachedJobVector;
        }

        job.JobEmbeddingStatus = JobEmbeddingStatusProcessing;
        job.JobEmbeddingError = null;
        job.JobEmbeddingUpdatedAt = DbDateTime.Now;

        EmbeddingGenerationResult result = await _embeddingProvider.GenerateEmbeddingAsync(jobText, cancellationToken);
        if (!result.Succeeded)
        {
            job.JobEmbeddingStatus = JobEmbeddingStatusFailed;
            job.JobEmbeddingError = result.FailureReason;
            throw new InvalidOperationException($"Job embedding failed: {result.FailureReason}");
        }

        job.JobEmbeddingTextHash = jobHash;
        job.JobEmbeddingVectorJson = JsonSerializer.Serialize(result.Vector);
        job.JobEmbeddingStatus = JobEmbeddingStatusCompleted;
        job.JobEmbeddingError = null;
        job.JobEmbeddingUpdatedAt = DbDateTime.Now;
        _embeddingCache.Set(jobHash, result.Vector);
        return result.Vector;
    }

    private async Task MarkSemanticFailureAsync(Domain.Entities.Application application, string error, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            application.SemanticScore = null;
            application.FinalScore = application.RuleScore ?? application.FinalScore;
            application.ScoreStatus = ScoreStatusSemanticFailed;
            application.ScoreError = TextNormalizationHelper.NormalizeOptionalText(error);
            application.ScoredAt = DbDateTime.Now;

            CandidateProfile? profile = application.User.CandidateProfile;
            if (profile != null && string.IsNullOrWhiteSpace(profile.CandidateEmbeddingStatus))
            {
                profile.CandidateEmbeddingStatus = CandidateEmbeddingStatusFailed;
                profile.CandidateEmbeddingError = error;
                profile.CandidateEmbeddingUpdatedAt = DbDateTime.Now;
            }

            if (string.IsNullOrWhiteSpace(application.Job.JobEmbeddingStatus))
            {
                application.Job.JobEmbeddingStatus = JobEmbeddingStatusFailed;
                application.Job.JobEmbeddingError = error;
                application.Job.JobEmbeddingUpdatedAt = DbDateTime.Now;
            }

            await _applicationRepository.UpdateAsync(application);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    private static string BuildCandidateEmbeddingText(CandidateProfile profile)
    {
        StringBuilder builder = new();
        builder.AppendLine("Candidate Profile");
        AppendSection(builder, "Headline", profile.CurrentPosition);
        AppendSection(builder, "Summary", profile.Bio);

        List<string> skills = profile.CandidateSkillDetails
            .Where(detail => detail.Skill != null)
            .Select(detail => detail.YearsOfExperience.HasValue
                ? $"{detail.Skill.Name} ({detail.YearsOfExperience:0.#} years)"
                : detail.Skill.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (skills.Count == 0)
        {
            skills = profile.Skills.Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        AppendSection(builder, "Skills", string.Join(", ", skills));
        AppendSection(builder, "Experience", FlattenJsonArray(profile.ExperienceEntriesJson, ["title", "company", "bullets"]));
        AppendSection(builder, "Projects", string.Join(Environment.NewLine, profile.Projects.Select(project =>
            $"{project.Name}. Role: {project.Role}. Description: {project.Description}. Technologies: {FlattenJsonArray(project.TechnologiesJson, [])}")));
        AppendSection(builder, "Education", FlattenJsonArray(profile.EducationRecordsJson, ["school", "degree", "fieldOfStudy", "description"]));
        AppendSection(builder, "Certifications", FlattenJsonArray(profile.CertificationRecordsJson, ["name", "issuer"]));
        AppendSection(builder, "Languages", FlattenJsonArray(profile.LanguageRecordsJson, ["name", "proficiency"]));
        AppendSection(builder, "Parsed Resume Keywords", ExtractKeywordsFromParsedResume(profile.ParsedResumeJson));
        return builder.ToString().Trim();
    }

    private static string BuildJobEmbeddingText(Job job)
    {
        StringBuilder builder = new();
        builder.AppendLine("Job Profile");
        AppendSection(builder, "Title", job.Title);
        AppendSection(builder, "Short Pitch", job.ShortPitch);
        AppendSection(builder, "Description", job.Description);
        AppendSection(builder, "Requirements", job.Requirements);
        AppendSection(builder, "Responsibilities", job.Description);
        AppendSection(builder, "Required Skills", string.Join(", ", job.JobSkills.Where(skill => skill.IsRequired).Select(skill => skill.Skill.Name)));
        AppendSection(builder, "Nice-To-Have Skills", string.Join(", ", job.JobSkills.Where(skill => !skill.IsRequired).Select(skill => skill.Skill.Name)));
        AppendSection(builder, "Minimum Experience", job.MinExperienceYears.HasValue ? $"{job.MinExperienceYears} years" : null);
        AppendSection(builder, "Work Mode", job.WorkMode.ToString());
        AppendSection(builder, "Employment Type", job.EmploymentType.ToString());
        AppendSection(builder, "Location", job.Location);
        return builder.ToString().Trim();
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
            return string.Empty;
        }

        return string.Empty;
    }

    private static string? FlattenJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => string.Join(", ", element.EnumerateArray().Select(FlattenJsonValue).Where(value => !string.IsNullOrWhiteSpace(value))),
            _ => null
        };
    }

    private static string ComputeSha256(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
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

    private bool TryResolveStoredVector(string? vectorJson, string hash, out IReadOnlyList<double>? vector)
    {
        if (_embeddingCache.TryGet(hash, out IReadOnlyList<double>? cachedVector) && cachedVector != null)
        {
            vector = cachedVector;
            return true;
        }

        vector = TryDeserializeVector(vectorJson);
        if (vector == null)
        {
            return false;
        }

        _embeddingCache.Set(hash, vector);
        return true;
    }

    private static IReadOnlyList<double>? TryDeserializeVector(string? vectorJson)
    {
        if (string.IsNullOrWhiteSpace(vectorJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<double>>(vectorJson);
        }
        catch
        {
            return null;
        }
    }
}
