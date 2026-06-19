using AutoMapper;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Request.Candidate;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Services;

public class CandidateService : ICandidateService
{
    private readonly ICandidateProfileRepository _candidateRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISkillRepository _skillRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly IEmailService _emailService;
    private readonly IResumeTextExtractor _resumeTextExtractor;
    private readonly IResumeParsingAiProvider _resumeParsingAiProvider;
    private readonly IMapper _mapper;
    private readonly ILogger<CandidateService> _logger;
    private const string CandidateRoleName = "Candidate";
    private const string ImportedSourcePrefix = "[Imported Source]";
    private const string ImportedNotesPrefix = "[Imported Notes]";
    private const string CandidateLoginUrl = "http://localhost:5173/login";
    private const string ResumeParseStatusNotStarted = "NotStarted";
    private const string ResumeParseStatusTextExtractionFailed = "TextExtractionFailed";
    private const string ResumeParseStatusParsing = "Parsing";
    private const string ResumeParseStatusCompleted = "Completed";
    private const string ResumeParseStatusRetryPending = "RetryPending";
    private const string ResumeParseStatusFailed = "Failed";
    private const string ResumeEmbeddingStatusNotStarted = "NotStarted";
    private const int MinimumResumeTextLength = 50;
    private const string ResumeExtractionFailureMessage = "Unable to read the resume content. Please upload a text-based PDF or DOCX file. Image-based or scanned PDF files are not supported.";
    private const string ResumeAiRetryMessage = "Your CV was uploaded successfully, but the AI parser is temporarily unavailable. The system will retry parsing later.";
    private const string ResumeAiFailedMessage = "Your CV was uploaded successfully, but automatic parsing could not be completed right now. You can retry parsing later without uploading again.";
    private static readonly Regex PhonePattern = new(@"^0\d{9}$", RegexOptions.Compiled);
    private static readonly Regex EmailExtractorPattern = new(@"(?<email>[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhoneExtractorPattern = new(@"(?<phone>(?:\+?84|0)[\s\-.]?(?:\d[\s\-.]?){8,10})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex UrlPattern = new(@"https?://[^\s)]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex YearPattern = new(@"\b(19|20)\d{2}\b", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the CandidateService class.
    /// </summary>
    /// <param name="candidateRepository">The <paramref name="candidateRepository"/> value.</param>
    /// <param name="userRepository">The <paramref name="userRepository"/> value.</param>
    /// <param name="skillRepository">The <paramref name="skillRepository"/> value.</param>
    /// <param name="unitOfWork">The <paramref name="unitOfWork"/> value.</param>
    /// <param name="fileStorage">The <paramref name="fileStorage"/> value.</param>
    /// <param name="emailService">The <paramref name="emailService"/> value.</param>
    /// <param name="resumeTextExtractor">The <paramref name="resumeTextExtractor"/> value.</param>
    /// <param name="resumeParsingAiProvider">The <paramref name="resumeParsingAiProvider"/> value.</param>
    /// <param name="mapper">The <paramref name="mapper"/> value.</param>
    /// <param name="logger">The <paramref name="logger"/> value.</param>
    public CandidateService(
        ICandidateProfileRepository candidateRepository,
        IUserRepository userRepository,
        ISkillRepository skillRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        IEmailService emailService,
        IResumeTextExtractor resumeTextExtractor,
        IResumeParsingAiProvider resumeParsingAiProvider,
        IMapper mapper,
        ILogger<CandidateService> logger)
    {
        _candidateRepository = candidateRepository;
        _userRepository = userRepository;
        _skillRepository = skillRepository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _emailService = emailService;
        _resumeTextExtractor = resumeTextExtractor;
        _resumeParsingAiProvider = resumeParsingAiProvider;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Registers the requested data.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <param name="resumeStream">The <paramref name="resumeStream"/> value.</param>
    /// <param name="resumeFileName">The <paramref name="resumeFileName"/> value.</param>
    /// <param name="resumeContentType">The <paramref name="resumeContentType"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CandidateRegisterResponseDto>> RegisterAsync(CandidateRegisterRequest request, Stream? resumeStream, string? resumeFileName, string? resumeContentType = null)
    {
        string? uploadedObjectName = null;
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            User user = new()
            {
                Id = Guid.NewGuid(),
                Email = request.UserInfo.Email,
                FullName = request.UserInfo.FullName,
                Phone = request.UserInfo.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.UserInfo.PasswordHash),
                CreatedAt = DbDateTime.Now,
                UpdatedAt = DbDateTime.Now
            };

            await _userRepository.AddAsync(user);
            await AssignCandidateRoleAsync(user.Id);

            CandidateProfile? profile = null;
            if (request.Profile != null)
            {
                string? resumeObjectKey = null;
                CandidateResume? initialResume = null;
                if (resumeStream != null && !string.IsNullOrWhiteSpace(resumeFileName))
                {
                    uploadedObjectName = $"resumes/{user.Id}/{Guid.NewGuid()}_{Path.GetFileName(resumeFileName)}";
                    resumeObjectKey = await _fileStorage.UploadFileAsync(
                        resumeStream,
                        uploadedObjectName,
                        resumeContentType ?? "application/octet-stream");

                    initialResume = new CandidateResume
                    {
                        Id = Guid.NewGuid(),
                        FileName = Path.GetFileName(resumeFileName),
                        StorageKey = resumeObjectKey,
                        UploadDate = DbDateTime.Now,
                        Version = 1,
                        IsCurrent = true
                    };
                }

                profile = new CandidateProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CurrentPosition = request.Profile.CurrentPosition,
                    ExperienceYears = request.Profile.ExperienceYears,
                    Education = request.Profile.Education,
                    Address = request.Profile.Address,
                    Bio = request.Profile.Bio,
                    GithubUrl = request.Profile.GitHubUrl,
                    LinkedinUrl = request.Profile.LinkedInUrl,
                    ResumeUrl = resumeObjectKey,
                    ResumeParseStatus = ResumeParseStatusNotStarted,
                    CandidateEmbeddingStatus = ResumeEmbeddingStatusNotStarted
                };

                if (initialResume != null)
                {
                    initialResume.CandidateProfileId = profile.Id;
                    profile.Resumes.Add(initialResume);
                }

                await _candidateRepository.SaveAsync(profile);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Registered candidate user {UserId} with profile {ProfileId}.", user.Id, profile?.Id);

            CandidateRegisterResponseDto response = _mapper.Map<CandidateRegisterResponseDto>(user, options =>
            {
                options.Items["CandidateProfileId"] = profile?.Id ?? Guid.Empty;
            });

            return ApiResponse<CandidateRegisterResponseDto>.Created(response, "Candidate registered successfully");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            _logger.LogWarning("Candidate registration failed for email {Email}. Rolling back transaction.", request.UserInfo.Email);
            if (!string.IsNullOrWhiteSpace(uploadedObjectName))
            {
                await _fileStorage.DeleteFileAsync(uploadedObjectName);
            }

            throw;
        }
    }

    /// <summary>
    /// Retrieves candidates.
    /// </summary>
    /// <param name="page">The <paramref name="page"/> value.</param>
    /// <param name="pageSize">The <paramref name="pageSize"/> value.</param>
    /// <param name="keyword">The <paramref name="keyword"/> value.</param>
    /// <param name="status">The <paramref name="status"/> value.</param>
    /// <param name="source">The <paramref name="source"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<HrCandidatesResponseDto>> GetCandidatesAsync(int page, int pageSize, string? keyword, string? status, string? source)
    {
        (IReadOnlyList<CandidateProfile> candidates, int total) = await _candidateRepository.GetPagedAsync(page, pageSize, keyword);

        IEnumerable<CandidateProfile> visibleCandidates = candidates.Where(candidate =>
            candidate.User.Applications.Any() || IsProfileComplete(candidate));

        IEnumerable<HrCandidateListItemDto> items = visibleCandidates.Select(candidate =>
        {
            string[] names = candidate.User.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Domain.Entities.Application? latestApplication = candidate.User.Applications.OrderByDescending(application => application.AppliedAt).FirstOrDefault();
            return new HrCandidateListItemDto
            {
                Id = candidate.Id.ToString(),
                FirstName = names.FirstOrDefault() ?? candidate.User.FullName,
                LastName = names.Length > 1 ? string.Join(' ', names.Skip(1)) : string.Empty,
                FullName = candidate.User.FullName,
                Email = candidate.User.Email,
                AvatarUrl = candidate.User.AvatarUrl,
                Source = ResolveCandidateSource(candidate, source),
                AppliedDate = latestApplication?.AppliedAt?.ToString("yyyy-MM-dd") ?? string.Empty,
                Status = latestApplication?.Status.ToString() ?? "Applied"
            };
        });

        if (!string.IsNullOrWhiteSpace(status))
        {
            items = items.Where(candidate => candidate.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        return ApiResponse<HrCandidatesResponseDto>.Ok(new HrCandidatesResponseDto
        {
            Items = items.ToList(),
            Meta = PaginationMetaBuilder.Build(page, pageSize, total)
        });
    }

    /// <summary>
    /// Retrieves candidate detail.
    /// </summary>
    /// <param name="candidateId">The <paramref name="candidateId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<HrCandidateDetailDto>> GetCandidateDetailAsync(string candidateId)
    {
        if (!Guid.TryParse(candidateId, out Guid candidateGuid))
        {
            return ApiResponse<HrCandidateDetailDto>.NotFound("Candidate not found.");
        }

        CandidateProfile? profile = await _candidateRepository.GetHrDetailByIdAsync(candidateGuid);
        if (profile == null)
        {
            return ApiResponse<HrCandidateDetailDto>.NotFound("Candidate not found.");
        }

        CandidateProfileResponseDto baseProfile = await MapProfileAsync(profile);
        List<Domain.Entities.Application> orderedApplications = profile.User.Applications
            .OrderByDescending(application => application.AppliedAt)
            .ToList();

        List<HrCandidateApplicationHistoryItemDto> applicationHistory = orderedApplications
            .Select(application => new HrCandidateApplicationHistoryItemDto
            {
                ApplicationId = application.Id.ToString(),
                JobId = application.JobId.ToString(),
                JobTitle = application.Job.Title,
                DepartmentName = application.Job.Department?.Name ?? "RecruitPro",
                Status = application.Status.ToString(),
                AppliedAt = application.AppliedAt,
                InterviewCount = application.Interviews.Count
            })
            .ToList();

        List<HrCandidateInterviewHistoryItemDto> interviewHistory = orderedApplications
            .SelectMany(application => application.Interviews.Select(interview => new HrCandidateInterviewHistoryItemDto
            {
                InterviewId = interview.Id.ToString(),
                ApplicationId = application.Id.ToString(),
                JobId = application.JobId.ToString(),
                JobTitle = application.Job.Title,
                DepartmentName = application.Job.Department?.Name ?? "RecruitPro",
                InterviewDate = interview.InterviewDate,
                Status = (interview.Status ?? Domain.Enums.InterviewStatus.Scheduled).ToString(),
                Notes = interview.Notes
            }))
            .OrderByDescending(interview => interview.InterviewDate)
            .ToList();

        return ApiResponse<HrCandidateDetailDto>.Ok(new HrCandidateDetailDto
        {
            Profile = baseProfile.Profile,
            Skills = baseProfile.Skills,
            ExperienceEntries = baseProfile.ExperienceEntries,
            Projects = baseProfile.Projects,
            Educations = baseProfile.Educations,
            Certifications = baseProfile.Certifications,
            Languages = baseProfile.Languages,
            Resume = baseProfile.Resume,
            ResumeHistory = baseProfile.ResumeHistory,
            ApplicationHistory = applicationHistory,
            InterviewHistory = interviewHistory
        });
    }

    /// <summary>
    /// Generates import template.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public Task<CandidateImportTemplateDto> GenerateImportTemplateAsync()
    {
        using XLWorkbook workbook = new();
        IXLWorksheet worksheet = workbook.Worksheets.Add("Candidates");

        string[] headers = ["FullName", "Email", "PhoneNumber", "Source", "PositionApplied", "Notes"];
        for (int index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
            worksheet.Cell(1, index + 1).Style.Font.Bold = true;
        }

        worksheet.Cell(2, 1).Value = "Nguyen Van A";
        worksheet.Cell(2, 2).Value = "a@gmail.com";
        worksheet.Cell(2, 3).Value = "0909123456";
        worksheet.Cell(2, 4).Value = "LinkedIn";
        worksheet.Cell(2, 5).Value = "Backend Developer";
        worksheet.Cell(2, 6).Value = "Senior Java";
        worksheet.Columns().AdjustToContents();

        using MemoryStream stream = new();
        workbook.SaveAs(stream);

        return Task.FromResult(new CandidateImportTemplateDto
        {
            FileName = "candidate-import-template.xlsx",
            Content = stream.ToArray()
        });
    }

    /// <summary>
    /// Previews import.
    /// </summary>
    /// <param name="fileStream">The <paramref name="fileStream"/> value.</param>
    /// <param name="fileName">The <paramref name="fileName"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CandidateImportPreviewResponseDto>> PreviewImportAsync(Stream fileStream, string fileName)
    {
        List<CandidateImportPreviewDto> rows = await ParseAndValidateImportRowsAsync(fileStream, fileName);
        return ApiResponse<CandidateImportPreviewResponseDto>.Ok(BuildPreviewResponse(rows));
    }

    /// <summary>
    /// Imports candidates.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CandidateImportResultDto>> ImportCandidatesAsync(CandidateImportRequest request)
    {
        List<CandidateImportPreviewDto> rows = await ValidateRequestRowsAsync(request.Rows);
        List<CandidateImportPreviewDto> validRows = rows.Where(row => row.IsValid).ToList();

        if (validRows.Count == 0)
        {
            return ApiResponse<CandidateImportResultDto>.BadRequest("No valid rows available for import.");
        }

        List<string> createdCandidateIds = [];
        List<string> invitationEmails = [];
        List<(string Email, string FullName, string TemporaryPassword)> invitations = [];

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            foreach (CandidateImportPreviewDto row in validRows)
            {
                string temporaryPassword = CredentialUtility.GenerateTemporaryPassword();
                User user = new()
                {
                    Id = Guid.NewGuid(),
                    Email = row.Email.Trim(),
                    FullName = row.FullName.Trim(),
                    Phone = TextNormalizationHelper.NormalizeOptionalText(row.PhoneNumber),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
                    CreatedAt = DbDateTime.Now,
                    UpdatedAt = DbDateTime.Now
                };

                await _userRepository.AddAsync(user);
                await AssignCandidateRoleAsync(user.Id);

                CandidateProfile profile = new()
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CurrentPosition = TextNormalizationHelper.NormalizeOptionalText(row.PositionApplied),
                    Bio = BuildImportedBio(row.Source, row.Notes),
                    ResumeParseStatus = ResumeParseStatusNotStarted,
                    CandidateEmbeddingStatus = ResumeEmbeddingStatusNotStarted
                };

                await _candidateRepository.SaveAsync(profile);
                createdCandidateIds.Add(profile.Id.ToString());
                invitationEmails.Add(user.Email);
                invitations.Add((user.Email, user.FullName, temporaryPassword));
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        foreach ((string email, string fullName, string temporaryPassword) in invitations)
        {
            try
            {
                await _emailService.SendCandidateInvitationAsync(email, fullName, temporaryPassword, CandidateLoginUrl);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Candidate import completed but invitation email failed for {Email}.", email);
            }
        }

        return ApiResponse<CandidateImportResultDto>.Created(new CandidateImportResultDto
        {
            ImportedCount = validRows.Count,
            SkippedCount = rows.Count - validRows.Count,
            CreatedCandidateIds = createdCandidateIds,
            InvitationEmails = invitationEmails
        }, "Candidates imported successfully.");
    }

    /// <summary>
    /// Retrieves profile.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CandidateProfileResponseDto>> GetProfileAsync(Guid userId)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(profile));
    }

    /// <summary>
    /// Updates profile.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    public async Task<ApiResponse<CandidateProfileResponseDto>> UpdateProfileAsync(Guid userId, UpdateCandidateProfileRequest request)
    {
        for (int attempt = 0; attempt < 2; attempt += 1)
        {
            CandidateProfile profile = await GetProfileEntityForUpdateAsync(userId);
            await ApplyProfileUpdateAsync(profile, request);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(profile));
            }
            catch (DbUpdateConcurrencyException exception) when (attempt == 0)
            {
                await _unitOfWork.RollbackAsync();
                _logger.LogWarning(
                    exception,
                    "Retrying candidate profile update after concurrency conflict for user {UserId}. Entries: {Entries}",
                    userId,
                    string.Join(", ", exception.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            }
        }

        throw new InvalidOperationException("Candidate profile update failed after retry.");
    }

    /// <summary>
    /// Updates skills.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CandidateProfileResponseDto>> UpdateSkillsAsync(Guid userId, UpdateCandidateSkillsRequest request)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        List<CandidateSkillUpsertRequest> requestedSkills = request.Skills.Count > 0
            ? request.Skills
            : request.SkillIds.Select(skillId => new CandidateSkillUpsertRequest
            {
                SkillId = skillId
            }).ToList();

        await _unitOfWork.BeginTransactionAsync();
        await ReplaceCandidateSkillsAsync(profile.Id, requestedSkills);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        CandidateProfile refreshedProfile = await GetProfileEntityAsync(userId);
        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(refreshedProfile));
    }

    /// <summary>
    /// Creates experience.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CandidateProfileResponseDto>> CreateExperienceAsync(Guid userId, UpsertCandidateExperienceRequest request)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        List<CandidateExperienceDocument> experiences = LoadExperiences(profile);
        experiences.Add(MapExperienceRequest(request, null));
        profile.ExperienceEntriesJson = SerializeDocuments(experiences);

        await SaveProfileAsync(profile);
        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(profile));
    }

    /// <summary>
    /// Updates experience.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="experienceId">The <paramref name="experienceId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="NotFoundException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    public async Task<ApiResponse<CandidateProfileResponseDto>> UpdateExperienceAsync(Guid userId, string experienceId, UpsertCandidateExperienceRequest request)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        List<CandidateExperienceDocument> experiences = LoadExperiences(profile);
        CandidateExperienceDocument? existing = experiences.FirstOrDefault(item => item.Id.Equals(experienceId, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            throw new NotFoundException("Experience entry not found.");
        }

        int index = experiences.IndexOf(existing);
        experiences[index] = MapExperienceRequest(request, existing.Id);
        profile.ExperienceEntriesJson = SerializeDocuments(experiences);

        await SaveProfileAsync(profile);
        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(profile));
    }

    /// <summary>
    /// Deletes experience.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="experienceId">The <paramref name="experienceId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="NotFoundException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    public async Task<ApiResponse<CandidateProfileResponseDto>> DeleteExperienceAsync(Guid userId, string experienceId)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        List<CandidateExperienceDocument> experiences = LoadExperiences(profile);
        int removedCount = experiences.RemoveAll(item => item.Id.Equals(experienceId, StringComparison.OrdinalIgnoreCase));
        if (removedCount == 0)
        {
            throw new NotFoundException("Experience entry not found.");
        }

        profile.ExperienceEntriesJson = SerializeDocuments(experiences);

        await SaveProfileAsync(profile);
        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(profile));
    }

    /// <summary>
    /// Parses resume.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="resumeStream">The <paramref name="resumeStream"/> value.</param>
    /// <param name="fileName">The <paramref name="fileName"/> value.</param>
    /// <param name="contentType">The <paramref name="contentType"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CandidateResumeParseResponseDto>> ParseResumeAsync(Guid userId, Stream resumeStream, string fileName, string? contentType = null)
    {
        if (resumeStream == null || string.IsNullOrWhiteSpace(fileName))
        {
            return ApiResponse<CandidateResumeParseResponseDto>.BadRequest("Please select a valid resume file.");
        }

        CandidateProfile profile = await GetProfileEntityAsync(userId);
        await using MemoryStream bufferedResume = await CopyToMemoryAsync(resumeStream);
        string extractedText;
        try
        {
            extractedText = await ExtractResumeTextAsync(bufferedResume, fileName);
        }
        catch (NotSupportedException exception)
        {
            _logger.LogWarning(exception, "Unsupported resume format for parsing: {FileName}", fileName);
            return ApiResponse<CandidateResumeParseResponseDto>.BadRequest(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to parse resume file {FileName} for user {UserId}.", fileName, userId);
            return ApiResponse<CandidateResumeParseResponseDto>.BadRequest("We could not read this resume file. Please upload a PDF or DOCX resume.");
        }

        extractedText = NormalizeResumeText(extractedText);
        if (!HasUsableResumeText(extractedText))
        {
            return ApiResponse<CandidateResumeParseResponseDto>.BadRequest(ResumeExtractionFailureMessage);
        }

        IReadOnlyList<Skill> allSkills = await _skillRepository.GetAllAsync();
        ResumeParsingAiResult aiResult = await _resumeParsingAiProvider.TryParseResumeAsync(extractedText, allSkills);
        CandidateResumeParseResponseDto preview = aiResult.UsedAi && aiResult.Data != null
            ? BuildResumeParsePreviewFromAi(aiResult.Data, extractedText, allSkills, profile, aiResult.ModelName)
            : BuildResumeParsePreview(extractedText, allSkills, profile);
        preview.ModelName ??= aiResult.ModelName;
        preview.AiFallbackReason = aiResult.UsedAi ? null : aiResult.FailureReason;
        preview.Notes = NormalizeParserWarnings(preview.Notes);
        return ApiResponse<CandidateResumeParseResponseDto>.Ok(
            preview,
            aiResult.UsedAi
                ? "Resume parsed with AI assistance. Please review and confirm the extracted information."
                : "Resume parsed with fallback parsing. Please review and confirm the extracted information.");
    }

    /// <summary>
    /// Uploads resume.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="resumeStream">The <paramref name="resumeStream"/> value.</param>
    /// <param name="fileName">The <paramref name="fileName"/> value.</param>
    /// <param name="contentType">The <paramref name="contentType"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ResumeUploadResponseDto>> UploadResumeAsync(Guid userId, Stream resumeStream, string fileName, string contentType)
    {
        if (resumeStream == null || string.IsNullOrWhiteSpace(fileName))
        {
            return ApiResponse<ResumeUploadResponseDto>.BadRequest("Please select a valid resume file.");
        }

        try
        {
            ValidateSupportedResumeFile(fileName);
        }
        catch (NotSupportedException exception)
        {
            return ApiResponse<ResumeUploadResponseDto>.BadRequest(exception.Message);
        }

        CandidateProfile profile = await GetProfileEntityAsync(userId);
        DateTime uploadedAt = DbDateTime.Now;
        await using MemoryStream bufferedResume = await CopyToMemoryAsync(resumeStream);
        string objectName = $"resumes/{userId}/{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        bufferedResume.Position = 0;
        string uploadedObjectKey = await _fileStorage.UploadFileAsync(bufferedResume, objectName, contentType);
        foreach (CandidateResume existingResume in profile.Resumes)
        {
            existingResume.IsCurrent = false;
        }

        int nextVersion = profile.Resumes.Count == 0 ? 1 : profile.Resumes.Max(resume => resume.Version) + 1;
        CandidateResume candidateResume = new()
        {
            CandidateProfileId = profile.Id,
            FileName = Path.GetFileName(fileName),
            StorageKey = uploadedObjectKey,
            UploadDate = uploadedAt,
            Version = nextVersion,
            IsCurrent = true
        };

        profile.Resumes.Add(candidateResume);
        profile.ResumeUrl = uploadedObjectKey;
        profile.User.UpdatedAt = uploadedAt;
        profile.ResumeParseStatus = ResumeParseStatusParsing;
        profile.ResumeParseError = null;
        profile.ResumeParseModel = null;
        profile.ResumeParsedAt = null;
        profile.ResumeParserWarningsJson = SerializeDocuments(new List<string>());
        profile.ResumeExtractedText = null;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _candidateRepository.UpdateAsync(profile);
            await _userRepository.UpdateAsync(profile.User);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            await _fileStorage.DeleteFileAsync(uploadedObjectKey);
            throw;
        }

        _logger.LogInformation("Updated resume for candidate user {UserId} with profile {ProfileId}.", userId, profile.Id);

        IReadOnlyList<Skill> allSkills = await _skillRepository.GetAllAsync();
        ResumeUploadResponseDto response = new()
        {
            ResumeId = candidateResume.Id.ToString(),
            FileName = candidateResume.FileName,
            UploadedAt = uploadedAt,
            Version = candidateResume.Version,
            IsCurrent = true,
            ParseStatus = ResumeParseStatusParsing
        };

        string extractedText;
        try
        {
            bufferedResume.Position = 0;
            extractedText = NormalizeResumeText(await ExtractResumeTextAsync(bufferedResume, fileName));
        }
        catch (Exception exception) when (exception is not NotSupportedException)
        {
            _logger.LogError(exception, "Resume extraction failed after upload for user {UserId} and file {FileName}.", userId, fileName);
            await PersistResumeParsingFailureAsync(profile, ResumeParseStatusTextExtractionFailed, ResumeExtractionFailureMessage, null, []);
            response.ParseStatus = ResumeParseStatusTextExtractionFailed;
            response.ParseMessage = ResumeExtractionFailureMessage;
            return ApiResponse<ResumeUploadResponseDto>.BadRequest(response.ParseMessage);
        }

        if (!HasUsableResumeText(extractedText))
        {
            await PersistResumeParsingFailureAsync(profile, ResumeParseStatusTextExtractionFailed, ResumeExtractionFailureMessage, extractedText, []);
            response.ParseStatus = ResumeParseStatusTextExtractionFailed;
            response.ParseMessage = ResumeExtractionFailureMessage;
            return ApiResponse<ResumeUploadResponseDto>.BadRequest(response.ParseMessage);
        }

        ResumeParsingAiResult aiResult = await _resumeParsingAiProvider.TryParseResumeAsync(extractedText, allSkills);
        if (aiResult.UsedAi && aiResult.Data != null)
        {
            CandidateResumeParseResponseDto preview = BuildResumeParsePreviewFromAi(aiResult.Data, extractedText, allSkills, profile, aiResult.ModelName);
            await PersistParsedResumeAsync(profile, preview, aiResult.Data, extractedText, aiResult.ModelName);
            response.ParseStatus = ResumeParseStatusCompleted;
            response.ParseMessage = "Resume uploaded and parsed successfully.";
            response.ParsedAt = profile.ResumeParsedAt;
            response.ParserWarnings = NormalizeParserWarnings(preview.Notes);
            return ApiResponse<ResumeUploadResponseDto>.Ok(response, response.ParseMessage);
        }

        CandidateResumeParseResponseDto fallbackPreview = BuildResumeParsePreview(extractedText, allSkills, profile);
        if (aiResult.IsRetryable)
        {
            LogRetryableAiFailure(aiResult);
            await PersistResumeParsingFailureAsync(profile, ResumeParseStatusRetryPending, aiResult.FailureReason, extractedText, fallbackPreview.Notes, aiResult.ModelName);
            response.ParseStatus = ResumeParseStatusRetryPending;
            response.ParseMessage = ResumeAiRetryMessage;
            response.ParserWarnings = NormalizeParserWarnings(fallbackPreview.Notes);
            return ApiResponse<ResumeUploadResponseDto>.Ok(response, response.ParseMessage);
        }

        await PersistResumeParsingFailureAsync(profile, ResumeParseStatusFailed, aiResult.FailureReason, extractedText, fallbackPreview.Notes, aiResult.ModelName);
        response.ParseStatus = ResumeParseStatusFailed;
        response.ParseMessage = ResumeAiFailedMessage;
        response.ParserWarnings = NormalizeParserWarnings(fallbackPreview.Notes);
        return ApiResponse<ResumeUploadResponseDto>.Ok(response, response.ParseMessage);
    }

    /// <summary>
    /// Retrieves resume download url.
    /// </summary>
    /// <param name="resumeId">The <paramref name="resumeId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ResumeFileResponseDto>> GetResumeDownloadUrlAsync(string resumeId)
    {
        if (!Guid.TryParse(resumeId, out Guid resumeGuid))
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Resume not found.");
        }

        CandidateProfile? profile = await _candidateRepository.GetByResumeIdAsync(resumeGuid);
        CandidateResume? resume = profile?.Resumes.FirstOrDefault(item => item.Id == resumeGuid);
        if (profile == null || resume == null)
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Resume not found.");
        }

        string presignedUrl = await _fileStorage.GetPresignedUrlAsync(resume.StorageKey);
        return ApiResponse<ResumeFileResponseDto>.Ok(new ResumeFileResponseDto
        {
            ResumeId = resume.Id.ToString(),
            FileName = resume.FileName,
            FileUrl = presignedUrl
        });
    }

    /// <summary>
    /// Retrieves profile entity.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="NotFoundException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    private async Task<CandidateProfile> GetProfileEntityAsync(Guid userId)
    {
        CandidateProfile? profile = await _candidateRepository.GetByUserIdAsync(userId);
        if (profile == null)
        {
            throw new NotFoundException("Candidate profile not found.");
        }

        return profile;
    }

    /// <summary>
    /// Retrieves profile entity for update.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="NotFoundException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    private async Task<CandidateProfile> GetProfileEntityForUpdateAsync(Guid userId)
    {
        CandidateProfile? profile = await _candidateRepository.GetByUserIdForUpdateAsync(userId);
        if (profile == null)
        {
            throw new NotFoundException("Candidate profile not found.");
        }

        return profile;
    }

    /// <summary>
    /// Maps profile.
    /// </summary>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<CandidateProfileResponseDto> MapProfileAsync(CandidateProfile profile)
    {
        List<CandidateExperienceDocument> experiences = LoadExperiences(profile);
        List<CandidateEducationDocument> educations = LoadDocuments<CandidateEducationDocument>(profile.EducationRecordsJson);
        List<CandidateCertificationDocument> certifications = LoadDocuments<CandidateCertificationDocument>(profile.CertificationRecordsJson);
        List<CandidateLanguageDocument> languages = LoadDocuments<CandidateLanguageDocument>(profile.LanguageRecordsJson);
        List<CandidateResumeDto> resumeHistory = [];

        foreach (CandidateResume candidateResume in profile.Resumes.OrderByDescending(item => item.Version))
        {
            resumeHistory.Add(await MapResumeAsync(candidateResume));
        }

        if (resumeHistory.Count == 0 && !string.IsNullOrWhiteSpace(profile.ResumeUrl))
        {
            resumeHistory.Add(new CandidateResumeDto
            {
                Id = profile.Id.ToString(),
                FileName = StoredFileNameHelper.ExtractDisplayFileName(profile.ResumeUrl),
                FileUrl = await _fileStorage.GetPresignedUrlAsync(profile.ResumeUrl),
                UploadedAt = profile.User.UpdatedAt ?? profile.User.CreatedAt ?? DbDateTime.Now,
                Version = 1,
                IsCurrent = true
            });
        }

        CandidateResumeDto? resume = resumeHistory.FirstOrDefault(item => item.IsCurrent) ?? resumeHistory.FirstOrDefault();
        decimal completionScore = CalculateProfileCompletionScore(profile, experiences, educations, certifications, languages, resume);

        return new CandidateProfileResponseDto
        {
            Profile = new CandidateProfileViewDto
            {
                Id = profile.Id.ToString(),
                Name = profile.User.FullName,
                AvatarUrl = profile.User.AvatarUrl,
                Headline = profile.CurrentPosition ?? string.Empty,
                Email = profile.User.Email,
                Phone = profile.User.Phone,
                Location = profile.Address ?? string.Empty,
                MemberSince = (profile.User.CreatedAt ?? DbDateTime.Now).ToString("yyyy-MM-dd"),
                Bio = profile.Bio,
                Github = profile.GithubUrl,
                Linkedin = profile.LinkedinUrl,
                CompletionScore = completionScore
            },
            Skills = profile.CandidateSkills
                .Where(candidateSkill => candidateSkill.Skill != null)
                .Select(candidateSkill => new CandidateSkillViewDto
            {
                Id = candidateSkill.SkillId.ToString(),
                Label = candidateSkill.Skill.Name,
                Active = true,
                YearsOfExperience = candidateSkill.YearsOfExperience
            }).ToList(),
            ExperienceEntries = experiences
                .OrderByDescending(item => item.Period.StartYear)
                .ThenByDescending(item => item.Period.StartMonth)
                .Select(item => new CandidateExperienceDto
                {
                    Id = item.Id,
                    Title = item.Title,
                    Company = item.Company,
                    Period = new CandidateExperiencePeriodDto
                    {
                        StartMonth = item.Period.StartMonth,
                        StartYear = item.Period.StartYear,
                        EndMonth = item.Period.EndMonth,
                        EndYear = item.Period.EndYear,
                        IsCurrent = item.Period.IsCurrent
                    },
                    Bullets = item.Bullets
                })
                .ToList(),
            Projects = profile.Projects
                .OrderByDescending(item => item.StartYear)
                .ThenByDescending(item => item.StartMonth)
                .Select(item => new CandidateProjectDto
                {
                    Id = item.Id.ToString(),
                    Name = item.Name,
                    Role = item.Role,
                    Description = item.Description,
                    Technologies = LoadStringList(item.TechnologiesJson),
                    Period = new CandidateExperiencePeriodDto
                    {
                        StartMonth = item.StartMonth,
                        StartYear = item.StartYear,
                        EndMonth = item.EndMonth,
                        EndYear = item.EndYear,
                        IsCurrent = item.IsCurrent
                    }
                })
                .ToList(),
            Educations = educations.Select(item => new CandidateEducationDto
            {
                Id = item.Id,
                School = item.School,
                Degree = item.Degree,
                FieldOfStudy = item.FieldOfStudy,
                StartYear = item.StartYear,
                EndYear = item.EndYear,
                Description = item.Description
            }).ToList(),
            Certifications = certifications.Select(item => new CandidateCertificationDto
            {
                Id = item.Id,
                Name = item.Name,
                Issuer = item.Issuer,
                IssuedOn = item.IssuedOn,
                ExpiresOn = item.ExpiresOn,
                CredentialId = item.CredentialId,
                CredentialUrl = item.CredentialUrl
            }).ToList(),
            Languages = languages.Select(item => new CandidateLanguageDto
            {
                Id = item.Id,
                Name = item.Name,
                Proficiency = item.Proficiency
            }).ToList(),
            Resume = resume,
            ResumeHistory = resumeHistory,
            ResumeParsing = new CandidateResumeParsingStatusDto
            {
                Status = string.IsNullOrWhiteSpace(profile.ResumeParseStatus) ? ResumeParseStatusNotStarted : profile.ResumeParseStatus,
                Error = profile.ResumeParseError,
                Model = profile.ResumeParseModel,
                ParsedAt = profile.ResumeParsedAt,
                Warnings = LoadStringList(profile.ResumeParserWarningsJson)
            }
        };
    }

    /// <summary>
    /// Saves profile.
    /// </summary>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SaveProfileAsync(CandidateProfile profile)
    {
        await _unitOfWork.BeginTransactionAsync();
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Applies profile update.
    /// </summary>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ApplyProfileUpdateAsync(CandidateProfile profile, UpdateCandidateProfileRequest request)
    {
        profile.User.FullName = request.Name ?? profile.User.FullName;
        profile.User.Email = request.Email ?? profile.User.Email;
        profile.User.Phone = request.Phone ?? profile.User.Phone;
        profile.User.UpdatedAt = DbDateTime.Now;
        profile.CurrentPosition = request.Headline ?? profile.CurrentPosition;
        profile.Address = request.Location ?? profile.Address;
        profile.Bio = request.Bio ?? profile.Bio;
        profile.GithubUrl = request.Github ?? profile.GithubUrl;
        profile.LinkedinUrl = request.Linkedin ?? profile.LinkedinUrl;

        if (request.Skills != null)
        {
            await ReplaceCandidateSkillsAsync(profile.Id, request.Skills);
        }

        if (request.ExperienceEntries != null)
        {
            profile.ExperienceEntriesJson = SerializeDocuments(request.ExperienceEntries.Select(MapExperienceRequest).ToList());
        }

        if (request.Projects != null)
        {
            await ReplaceCandidateProjectsAsync(profile.Id, request.Projects);
        }

        if (request.Educations != null)
        {
            profile.EducationRecordsJson = SerializeDocuments(request.Educations.Select(MapEducationRequest).ToList());
        }

        if (request.Certifications != null)
        {
            profile.CertificationRecordsJson = SerializeDocuments(request.Certifications.Select(MapCertificationRequest).ToList());
        }

        if (request.Languages != null)
        {
            profile.LanguageRecordsJson = SerializeDocuments(request.Languages.Select(MapLanguageRequest).ToList());
        }
    }

    /// <summary>
    /// Replaces candidate projects.
    /// </summary>
    /// <param name="candidateProfileId">The <paramref name="candidateProfileId"/> value.</param>
    /// <param name="requestedProjects">The <paramref name="requestedProjects"/> value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ReplaceCandidateProjectsAsync(
        Guid candidateProfileId,
        IEnumerable<CandidateProjectUpsertRequest> requestedProjects)
    {
        List<CandidateProject> projects = requestedProjects
            .Select(MapProjectRequest)
            .ToList();

        await _candidateRepository.ReplaceProjectsAsync(candidateProfileId, projects);
    }

    /// <summary>
    /// Loads experiences.
    /// </summary>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CandidateExperienceDocument> LoadExperiences(CandidateProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.ExperienceEntriesJson))
        {
            return LoadDocuments<CandidateExperienceDocument>(profile.ExperienceEntriesJson);
        }

        if (string.IsNullOrWhiteSpace(profile.Education))
        {
            return [];
        }

        try
        {
            List<CandidateExperienceDocument>? parsed = JsonSerializer.Deserialize<List<CandidateExperienceDocument>>(profile.Education);
            return parsed ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string? SerializeDocuments<TDocument>(List<TDocument> documents)
    {
        return documents.Count == 0 ? null : JsonSerializer.Serialize(documents);
    }

    /// <summary>
    /// Maps experience request.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <param name="existingId">The <paramref name="existingId"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CandidateExperienceDocument MapExperienceRequest(UpsertCandidateExperienceRequest request, string? existingId)
    {
        return new CandidateExperienceDocument
        {
            Id = existingId ?? Guid.NewGuid().ToString("N"),
            Title = request.Title.Trim(),
            Company = request.Company.Trim(),
            Period = new CandidateExperiencePeriodDocument
            {
                StartMonth = request.Period.StartMonth,
                StartYear = request.Period.StartYear,
                EndMonth = request.Period.IsCurrent ? null : request.Period.EndMonth,
                EndYear = request.Period.IsCurrent ? null : request.Period.EndYear,
                IsCurrent = request.Period.IsCurrent
            },
            Bullets = request.Bullets
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList()
        };
    }

    /// <summary>
    /// Maps experience request.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CandidateExperienceDocument MapExperienceRequest(CandidateExperienceUpsertItemRequest request)
    {
        return new CandidateExperienceDocument
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
            Title = request.Title.Trim(),
            Company = request.Company.Trim(),
            Period = new CandidateExperiencePeriodDocument
            {
                StartMonth = request.Period.StartMonth,
                StartYear = request.Period.StartYear,
                EndMonth = request.Period.IsCurrent ? null : request.Period.EndMonth,
                EndYear = request.Period.IsCurrent ? null : request.Period.EndYear,
                IsCurrent = request.Period.IsCurrent
            },
            Bullets = request.Bullets
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList()
        };
    }

    private static CandidateExperienceDocument MapExperienceDto(CandidateExperienceDto request)
    {
        return new CandidateExperienceDocument
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
            Title = request.Title.Trim(),
            Company = request.Company.Trim(),
            Period = new CandidateExperiencePeriodDocument
            {
                StartMonth = request.Period.StartMonth,
                StartYear = request.Period.StartYear,
                EndMonth = request.Period.IsCurrent ? null : request.Period.EndMonth,
                EndYear = request.Period.IsCurrent ? null : request.Period.EndYear,
                IsCurrent = request.Period.IsCurrent
            },
            Bullets = request.Bullets
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList()
        };
    }

    /// <summary>
    /// Maps project request.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CandidateProject MapProjectRequest(CandidateProjectUpsertRequest request)
    {
        return new CandidateProject
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid() : Guid.Parse(request.Id),
            Name = request.Name.Trim(),
            Role = TextNormalizationHelper.NormalizeOptionalText(request.Role),
            Description = TextNormalizationHelper.NormalizeOptionalText(request.Description),
            TechnologiesJson = SerializeDocuments(request.Technologies
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()),
            StartMonth = request.Period.StartMonth,
            StartYear = request.Period.StartYear,
            EndMonth = request.Period.IsCurrent ? null : request.Period.EndMonth,
            EndYear = request.Period.IsCurrent ? null : request.Period.EndYear,
            IsCurrent = request.Period.IsCurrent
        };
    }

    /// <summary>
    /// Maps education request.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CandidateEducationDocument MapEducationRequest(CandidateEducationUpsertRequest request)
    {
        return new CandidateEducationDocument
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
            School = request.School.Trim(),
            Degree = request.Degree.Trim(),
            FieldOfStudy = TextNormalizationHelper.NormalizeOptionalText(request.FieldOfStudy),
            StartYear = request.StartYear,
            EndYear = request.EndYear,
            Description = TextNormalizationHelper.NormalizeOptionalText(request.Description)
        };
    }

    private static CandidateEducationDocument MapEducationDto(CandidateEducationDto request)
    {
        return new CandidateEducationDocument
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
            School = request.School.Trim(),
            Degree = request.Degree.Trim(),
            FieldOfStudy = TextNormalizationHelper.NormalizeOptionalText(request.FieldOfStudy),
            StartYear = request.StartYear,
            EndYear = request.EndYear,
            Description = TextNormalizationHelper.NormalizeOptionalText(request.Description)
        };
    }

    /// <summary>
    /// Maps certification request.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CandidateCertificationDocument MapCertificationRequest(CandidateCertificationUpsertRequest request)
    {
        return new CandidateCertificationDocument
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
            Name = request.Name.Trim(),
            Issuer = TextNormalizationHelper.NormalizeOptionalText(request.Issuer),
            IssuedOn = request.IssuedOn,
            ExpiresOn = request.ExpiresOn,
            CredentialId = TextNormalizationHelper.NormalizeOptionalText(request.CredentialId),
            CredentialUrl = TextNormalizationHelper.NormalizeOptionalText(request.CredentialUrl)
        };
    }

    private static CandidateCertificationDocument MapCertificationDto(CandidateCertificationDto request)
    {
        return new CandidateCertificationDocument
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
            Name = request.Name.Trim(),
            Issuer = TextNormalizationHelper.NormalizeOptionalText(request.Issuer),
            IssuedOn = request.IssuedOn,
            ExpiresOn = request.ExpiresOn,
            CredentialId = TextNormalizationHelper.NormalizeOptionalText(request.CredentialId),
            CredentialUrl = TextNormalizationHelper.NormalizeOptionalText(request.CredentialUrl)
        };
    }

    /// <summary>
    /// Maps language request.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CandidateLanguageDocument MapLanguageRequest(CandidateLanguageUpsertRequest request)
    {
        return new CandidateLanguageDocument
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
            Name = request.Name.Trim(),
            Proficiency = request.Proficiency.Trim()
        };
    }

    private static CandidateLanguageDocument MapLanguageDto(CandidateLanguageDto request)
    {
        return new CandidateLanguageDocument
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
            Name = request.Name.Trim(),
            Proficiency = request.Proficiency.Trim()
        };
    }

    /// <summary>
    /// Replaces candidate skills.
    /// </summary>
    /// <param name="candidateProfileId">The <paramref name="candidateProfileId"/> value.</param>
    /// <param name="requestedSkills">The <paramref name="requestedSkills"/> value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ReplaceCandidateSkillsAsync(Guid candidateProfileId, List<CandidateSkillUpsertRequest> requestedSkills)
    {
        List<Guid> skillIds = requestedSkills
            .Select(value => Guid.TryParse(value.SkillId, out Guid parsed) ? parsed : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToList();
        IReadOnlyList<Skill> resolvedSkills = await _skillRepository.GetByIdsAsync(skillIds);
        List<CandidateSkill> candidateSkills = [];

        foreach (Skill skill in resolvedSkills)
        {
            CandidateSkillUpsertRequest? request = requestedSkills.FirstOrDefault(item =>
                Guid.TryParse(item.SkillId, out Guid parsedSkillId) && parsedSkillId == skill.Id);

            candidateSkills.Add(new CandidateSkill
            {
                CandidateId = candidateProfileId,
                SkillId = skill.Id,
                Skill = skill,
                YearsOfExperience = request?.YearsOfExperience
            });
        }

        await _candidateRepository.ReplaceSkillsAsync(candidateProfileId, candidateSkills);
    }

    private async Task PersistParsedResumeAsync(
        CandidateProfile profile,
        CandidateResumeParseResponseDto preview,
        CandidateResumeAiParseDto rawAiData,
        string extractedText,
        string? modelName)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await ApplyParsedResumeToProfileAsync(profile, preview);
            profile.ParsedResumeJson = JsonSerializer.Serialize(rawAiData);
            profile.ResumeExtractedText = extractedText;
            profile.ResumeParseStatus = ResumeParseStatusCompleted;
            profile.ResumeParseError = null;
            profile.ResumeParseModel = modelName;
            profile.ResumeParserWarningsJson = SerializeDocuments(NormalizeParserWarnings(preview.Notes));
            profile.ResumeParsedAt = DbDateTime.Now;
            profile.CandidateEmbeddingStatus ??= ResumeEmbeddingStatusNotStarted;
            await _candidateRepository.UpdateAsync(profile);
            await _userRepository.UpdateAsync(profile.User);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    private async Task PersistResumeParsingFailureAsync(
        CandidateProfile profile,
        string status,
        string? error,
        string? extractedText,
        IEnumerable<string> warnings,
        string? modelName = null)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            profile.ResumeExtractedText = extractedText;
            profile.ResumeParseStatus = status;
            profile.ResumeParseError = TextNormalizationHelper.NormalizeOptionalText(error);
            profile.ResumeParseModel = modelName;
            profile.ResumeParserWarningsJson = SerializeDocuments(NormalizeParserWarnings(warnings));
            profile.ResumeParsedAt = status == ResumeParseStatusCompleted ? DbDateTime.Now : null;
            profile.CandidateEmbeddingStatus ??= ResumeEmbeddingStatusNotStarted;
            await _candidateRepository.UpdateAsync(profile);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    private async Task ApplyParsedResumeToProfileAsync(CandidateProfile profile, CandidateResumeParseResponseDto preview)
    {
        profile.User.FullName = string.IsNullOrWhiteSpace(preview.Profile.Name) ? profile.User.FullName : preview.Profile.Name.Trim();
        profile.User.Email = string.IsNullOrWhiteSpace(preview.Profile.Email) ? profile.User.Email : preview.Profile.Email.Trim();
        profile.User.Phone = string.IsNullOrWhiteSpace(preview.Profile.Phone) ? profile.User.Phone : preview.Profile.Phone.Trim();
        profile.User.UpdatedAt = DbDateTime.Now;
        profile.CurrentPosition = TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Headline) ?? profile.CurrentPosition;
        profile.Address = TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Location) ?? profile.Address;
        profile.Bio = TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Bio) ?? profile.Bio;
        profile.GithubUrl = TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Github) ?? profile.GithubUrl;
        profile.LinkedinUrl = TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Linkedin) ?? profile.LinkedinUrl;
        profile.ExperienceYears = CalculateExperienceYears(preview.ExperienceEntries);
        profile.ExperienceEntriesJson = SerializeDocuments(preview.ExperienceEntries.Select(MapExperienceDto).ToList());
        profile.EducationRecordsJson = SerializeDocuments(preview.Educations.Select(MapEducationDto).ToList());
        profile.CertificationRecordsJson = SerializeDocuments(preview.Certifications.Select(MapCertificationDto).ToList());
        profile.LanguageRecordsJson = SerializeDocuments(preview.Languages.Select(MapLanguageDto).ToList());

        await ReplaceCandidateProjectsAsync(profile.Id, preview.Projects.Select(project => new CandidateProjectUpsertRequest
        {
            Id = project.Id,
            Name = project.Name,
            Role = project.Role,
            Description = project.Description,
            Technologies = project.Technologies,
            Period = new CandidateExperiencePeriodRequest
            {
                StartMonth = project.Period.StartMonth,
                StartYear = project.Period.StartYear,
                EndMonth = project.Period.EndMonth,
                EndYear = project.Period.EndYear,
                IsCurrent = project.Period.IsCurrent
            }
        }));

        await ReplaceCandidateSkillsAsync(profile.Id, preview.Skills
            .Where(skill => Guid.TryParse(skill.Id, out _))
            .Select(skill => new CandidateSkillUpsertRequest
            {
                SkillId = skill.Id,
                YearsOfExperience = skill.YearsOfExperience
            })
            .ToList());
    }

    private static int? CalculateExperienceYears(IEnumerable<CandidateExperienceDto> experiences)
    {
        double totalMonths = 0;
        foreach (CandidateExperienceDto experience in experiences)
        {
            int startMonth = Math.Clamp(experience.Period.StartMonth, 1, 12);
            int startYear = experience.Period.StartYear;
            int endMonth = experience.Period.IsCurrent ? DbDateTime.Now.Month : Math.Clamp(experience.Period.EndMonth ?? startMonth, 1, 12);
            int endYear = experience.Period.IsCurrent ? DbDateTime.Now.Year : experience.Period.EndYear ?? startYear;
            totalMonths += Math.Max(((endYear - startYear) * 12) + (endMonth - startMonth) + 1, 0);
        }

        if (totalMonths <= 0)
        {
            return null;
        }

        return (int)Math.Round(totalMonths / 12d, MidpointRounding.AwayFromZero);
    }

    private static List<string> NormalizeParserWarnings(IEnumerable<string>? warnings)
    {
        return (warnings ?? [])
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasUsableResumeText(string? extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return false;
        }

        return extractedText.Trim().Length >= MinimumResumeTextLength;
    }

    private static void ValidateSupportedResumeFile(string fileName)
    {
        string extension = Path.GetExtension(fileName).Trim().ToLowerInvariant();
        if (extension is ".pdf" or ".docx" or ".txt")
        {
            return;
        }

        throw new NotSupportedException("Unsupported resume format. Please upload a PDF, DOCX, or TXT file.");
    }

    private static async Task<MemoryStream> CopyToMemoryAsync(Stream source)
    {
        MemoryStream buffer = new();
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        await source.CopyToAsync(buffer);
        buffer.Position = 0;
        return buffer;
    }

    private void LogRetryableAiFailure(ResumeParsingAiResult aiResult)
    {
        _logger.LogWarning(
            "Resume parsing provider unavailable. Provider={Provider}, Model={Model}, StatusCode={StatusCode}, TraceId={TraceId}, Response={Response}",
            aiResult.Provider,
            aiResult.ModelName,
            aiResult.HttpStatusCode,
            aiResult.TraceId,
            aiResult.RawProviderResponse);
    }

    private static List<TDocument> LoadDocuments<TDocument>(string? jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return [];
        }

        try
        {
            List<TDocument>? parsed = JsonSerializer.Deserialize<List<TDocument>>(jsonString);
            return parsed ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Loads string list.
    /// </summary>
    /// <param name="jsonString">The <paramref name="jsonString"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<string> LoadStringList(string? jsonString)
    {
        return LoadDocuments<string>(jsonString)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    /// <summary>
    /// Maps resume.
    /// </summary>
    /// <param name="candidateResume">The <paramref name="candidateResume"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<CandidateResumeDto> MapResumeAsync(CandidateResume candidateResume)
    {
        return new CandidateResumeDto
        {
            Id = candidateResume.Id.ToString(),
            FileName = candidateResume.FileName,
            FileUrl = await _fileStorage.GetPresignedUrlAsync(candidateResume.StorageKey),
            UploadedAt = candidateResume.UploadDate,
            Version = candidateResume.Version,
            IsCurrent = candidateResume.IsCurrent
        };
    }

    /// <summary>
    /// Calculates profile completion score.
    /// </summary>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <param name="experiences">The <paramref name="experiences"/> value.</param>
    /// <param name="educations">The <paramref name="educations"/> value.</param>
    /// <param name="certifications">The <paramref name="certifications"/> value.</param>
    /// <param name="languages">The <paramref name="languages"/> value.</param>
    /// <param name="currentResume">The <paramref name="currentResume"/> value.</param>
    /// <returns>The operation result.</returns>
    private static decimal CalculateProfileCompletionScore(
        CandidateProfile profile,
        IReadOnlyCollection<CandidateExperienceDocument> experiences,
        IReadOnlyCollection<CandidateEducationDocument> educations,
        IReadOnlyCollection<CandidateCertificationDocument> certifications,
        IReadOnlyCollection<CandidateLanguageDocument> languages,
        CandidateResumeDto? currentResume)
    {
        decimal score = 0;

        if (!string.IsNullOrWhiteSpace(profile.User.FullName)
            && !string.IsNullOrWhiteSpace(profile.User.Email)
            && !string.IsNullOrWhiteSpace(profile.Address))
        {
            score += 20;
        }

        if (!string.IsNullOrWhiteSpace(profile.CurrentPosition) && !string.IsNullOrWhiteSpace(profile.Bio))
        {
            score += 15;
        }

        if (profile.CandidateSkills.Count > 0)
        {
            score += 20;
        }

        if (experiences.Count > 0)
        {
            score += 15;
        }

        if (profile.Projects.Count > 0)
        {
            score += 10;
        }

        if (educations.Count > 0)
        {
            score += 10;
        }

        if (certifications.Count > 0 || languages.Count > 0)
        {
            score += 5;
        }

        if (currentResume != null)
        {
            score += 5;
        }

        return Math.Min(score, 100);
    }

    /// <summary>
    /// Executes the is profile complete operation.
    /// </summary>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    private static bool IsProfileComplete(CandidateProfile profile)
    {
        List<CandidateExperienceDocument> experiences = LoadExperiences(profile);
        List<CandidateEducationDocument> educations = LoadDocuments<CandidateEducationDocument>(profile.EducationRecordsJson);
        List<CandidateCertificationDocument> certifications = LoadDocuments<CandidateCertificationDocument>(profile.CertificationRecordsJson);
        List<CandidateLanguageDocument> languages = LoadDocuments<CandidateLanguageDocument>(profile.LanguageRecordsJson);
        CandidateResumeDto? currentResume = profile.Resumes.Any(item => item.IsCurrent) || !string.IsNullOrWhiteSpace(profile.ResumeUrl)
            ? new CandidateResumeDto()
            : null;

        return CalculateProfileCompletionScore(profile, experiences, educations, certifications, languages, currentResume) >= 70;
    }

    /// <summary>
    /// Extracts resume text.
    /// </summary>
    /// <param name="resumeStream">The <paramref name="resumeStream"/> value.</param>
    /// <param name="fileName">The <paramref name="fileName"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="NotSupportedException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    private async Task<string> ExtractResumeTextAsync(Stream resumeStream, string fileName)
    {
        string extension = Path.GetExtension(fileName).Trim().ToLowerInvariant();
        if (resumeStream.CanSeek)
        {
            resumeStream.Position = 0;
        }

        return extension switch
        {
            ".pdf" => await _resumeTextExtractor.ExtractTextAsync(resumeStream),
            ".docx" => await ExtractDocxTextAsync(resumeStream),
            ".txt" => await ReadPlainTextAsync(resumeStream),
            ".doc" => throw new NotSupportedException("Legacy .doc resumes are not supported for parsing yet. Please upload a PDF or DOCX file."),
            _ => throw new NotSupportedException("Unsupported resume format. Please upload a PDF or DOCX file.")
        };
    }

    /// <summary>
    /// Executes the read plain text operation.
    /// </summary>
    /// <param name="stream">The <paramref name="stream"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private static async Task<string> ReadPlainTextAsync(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using StreamReader reader = new(stream, leaveOpen: true);
        string content = await reader.ReadToEndAsync();
        return NormalizeResumeText(content);
    }

    /// <summary>
    /// Extracts docx text.
    /// </summary>
    /// <param name="stream">The <paramref name="stream"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private static async Task<string> ExtractDocxTextAsync(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry? documentEntry = archive.GetEntry("word/document.xml");
        if (documentEntry == null)
        {
            return string.Empty;
        }

        await using Stream entryStream = documentEntry.Open();
        using StreamReader reader = new(entryStream);
        string xml = await reader.ReadToEndAsync();
        string text = Regex.Replace(xml, "<w:tab[^>]*/>", "\t", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</w:p>", Environment.NewLine, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        return NormalizeResumeText(text);
    }

    /// <summary>
    /// Builds resume parse preview.
    /// </summary>
    /// <param name="extractedText">The <paramref name="extractedText"/> value.</param>
    /// <param name="allSkills">The <paramref name="allSkills"/> value.</param>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <returns>The operation result.</returns>
    private CandidateResumeParseResponseDto BuildResumeParsePreview(string extractedText, IReadOnlyList<Skill> allSkills, CandidateProfile profile)
    {
        string normalizedText = NormalizeResumeText(extractedText);
        List<string> lines = normalizedText
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        Dictionary<string, List<string>> sections = ExtractResumeSections(lines);

        string email = ExtractEmail(normalizedText) ?? profile.User.Email;
        string phone = ExtractPhone(normalizedText) ?? profile.User.Phone ?? string.Empty;
        string github = ExtractUrl(normalizedText, "github.com") ?? profile.GithubUrl ?? string.Empty;
        string linkedin = ExtractUrl(normalizedText, "linkedin.com") ?? profile.LinkedinUrl ?? string.Empty;
        string name = ExtractCandidateName(lines) ?? profile.User.FullName;
        string headline = ExtractHeadline(lines, name) ?? profile.CurrentPosition ?? string.Empty;
        string location = ExtractLocation(lines) ?? profile.Address ?? string.Empty;
        string bio = ExtractSummary(sections) ?? profile.Bio ?? string.Empty;

        List<CandidateSkillViewDto> parsedSkills = MatchSkills(normalizedText, allSkills)
            .Select(skill => new CandidateSkillViewDto
            {
                Id = skill.Id.ToString(),
                Label = skill.Name,
                Active = true,
                YearsOfExperience = ExtractYearsOfExperienceForSkill(normalizedText, skill.Name)
            })
            .ToList();

        List<CandidateExperienceDto> experiences = ParseExperienceEntries(sections.TryGetValue("experience", out List<string>? experienceLines) ? experienceLines : lines, "Experience", defaultCompany: "Not specified");
        List<CandidateProjectDto> projects = ParseProjects(sections.TryGetValue("projects", out List<string>? projectLines) ? projectLines : []);
        List<CandidateEducationDto> educations = ParseEducations(sections.TryGetValue("education", out List<string>? educationLines) ? educationLines : []);
        List<CandidateCertificationDto> certifications = ParseCertifications(sections.TryGetValue("certifications", out List<string>? certificationLines) ? certificationLines : []);
        List<CandidateLanguageDto> languages = ParseLanguages(sections.TryGetValue("languages", out List<string>? languageLines) ? languageLines : []);

        List<string> notes = [];
        notes.Add($"Detected {parsedSkills.Count} skills from the resume.");
        if (experiences.Count > 0)
        {
            notes.Add($"Detected {experiences.Count} work experience entries.");
        }

        if (projects.Count > 0)
        {
            notes.Add($"Detected {projects.Count} projects.");
        }

        if (educations.Count > 0)
        {
            notes.Add($"Detected {educations.Count} education records.");
        }

        if (certifications.Count > 0)
        {
            notes.Add($"Detected {certifications.Count} certifications.");
        }

        if (languages.Count > 0)
        {
            notes.Add($"Detected {languages.Count} languages.");
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            notes.Add("Some personal information could not be confidently extracted. Please review carefully before saving.");
        }

        return new CandidateResumeParseResponseDto
        {
            UsedAi = false,
            ParsingMode = "Heuristic",
            Profile = new CandidateResumeParseProfileDto
            {
                Name = name,
                Headline = headline,
                Email = email,
                Phone = phone,
                Location = location,
                Bio = bio,
                Github = github,
                Linkedin = linkedin
            },
            Skills = parsedSkills,
            ExperienceEntries = experiences,
            Projects = projects,
            Educations = educations,
            Certifications = certifications,
            Languages = languages,
            Notes = notes,
            ExtractedTextPreview = string.Join(Environment.NewLine, lines.Take(40))
        };
    }

    /// <summary>
    /// Builds resume parse preview from ai.
    /// </summary>
    /// <param name="aiPreview">The <paramref name="aiPreview"/> value.</param>
    /// <param name="extractedText">The <paramref name="extractedText"/> value.</param>
    /// <param name="allSkills">The <paramref name="allSkills"/> value.</param>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <param name="modelName">The <paramref name="modelName"/> value.</param>
    /// <returns>The operation result.</returns>
    private CandidateResumeParseResponseDto BuildResumeParsePreviewFromAi(
        CandidateResumeAiParseDto aiPreview,
        string extractedText,
        IReadOnlyList<Skill> allSkills,
        CandidateProfile profile,
        string? modelName)
    {
        Dictionary<string, Skill> skillLookup = allSkills
            .GroupBy(skill => skill.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<CandidateSkillViewDto> parsedSkills = aiPreview.Skills
            .Where(skill => !string.IsNullOrWhiteSpace(skill.Name))
            .Select(skill =>
            {
                Skill? matchedSkill = skillLookup.TryGetValue(skill.Name.Trim(), out Skill? foundSkill)
                    ? foundSkill
                    : skillLookup.Values.FirstOrDefault(value =>
                        value.Name.Contains(skill.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                        || skill.Name.Trim().Contains(value.Name, StringComparison.OrdinalIgnoreCase));

                return new CandidateSkillViewDto
                {
                    Id = matchedSkill?.Id.ToString() ?? Guid.NewGuid().ToString(),
                    Label = matchedSkill?.Name ?? skill.Name.Trim(),
                    Active = true,
                    YearsOfExperience = skill.YearsOfExperience
                };
            })
            .DistinctBy(skill => skill.Label.ToLowerInvariant())
            .ToList();

        return new CandidateResumeParseResponseDto
        {
            UsedAi = true,
            ParsingMode = "AI",
            ModelName = modelName,
            Profile = new CandidateResumeParseProfileDto
            {
                Name = aiPreview.Profile.Name ?? profile.User.FullName,
                Headline = aiPreview.Profile.Headline ?? profile.CurrentPosition ?? string.Empty,
                Email = aiPreview.Profile.Email ?? profile.User.Email,
                Phone = aiPreview.Profile.Phone ?? profile.User.Phone ?? string.Empty,
                Location = aiPreview.Profile.Location ?? profile.Address ?? string.Empty,
                Bio = aiPreview.Profile.Summary ?? profile.Bio ?? string.Empty,
                Github = aiPreview.Profile.Github ?? profile.GithubUrl ?? string.Empty,
                Linkedin = aiPreview.Profile.Linkedin ?? profile.LinkedinUrl ?? string.Empty
            },
            Skills = parsedSkills,
            ExperienceEntries = aiPreview.ExperienceEntries
                .Select(item => new CandidateExperienceDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = string.IsNullOrWhiteSpace(item.Title) ? "Experience" : item.Title.Trim(),
                    Company = string.IsNullOrWhiteSpace(item.Company) ? "Not specified" : item.Company.Trim(),
                    Period = new CandidateExperiencePeriodDto
                    {
                        StartMonth = item.StartMonth ?? 1,
                        StartYear = item.StartYear ?? DateTime.UtcNow.Year,
                        EndMonth = item.IsCurrent ? null : item.EndMonth,
                        EndYear = item.IsCurrent ? null : item.EndYear,
                        IsCurrent = item.IsCurrent
                    },
                    Bullets = item.Bullets
                        .Select(value => value.Trim())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                .ToList(),
            Projects = aiPreview.Projects
                .Select(item => new CandidateProjectDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = string.IsNullOrWhiteSpace(item.Name) ? "Project" : item.Name.Trim(),
                    Role = TextNormalizationHelper.NormalizeOptionalText(item.Role),
                    Description = TextNormalizationHelper.NormalizeOptionalText(item.Description),
                    Technologies = item.Technologies
                        .Select(value => value.Trim())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    Period = new CandidateExperiencePeriodDto
                    {
                        StartMonth = item.StartMonth ?? 1,
                        StartYear = item.StartYear ?? DateTime.UtcNow.Year,
                        EndMonth = item.IsCurrent ? null : item.EndMonth,
                        EndYear = item.IsCurrent ? null : item.EndYear,
                        IsCurrent = item.IsCurrent
                    }
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .ToList(),
            Educations = aiPreview.Educations
                .Select(item => new CandidateEducationDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    School = item.School.Trim(),
                    Degree = item.Degree.Trim(),
                    FieldOfStudy = TextNormalizationHelper.NormalizeOptionalText(item.FieldOfStudy),
                    StartYear = item.StartYear,
                    EndYear = item.EndYear,
                    Description = TextNormalizationHelper.NormalizeOptionalText(item.Description)
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.School) && !string.IsNullOrWhiteSpace(item.Degree))
                .ToList(),
            Certifications = aiPreview.Certifications
                .Select(item => new CandidateCertificationDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = item.Name.Trim(),
                    Issuer = TextNormalizationHelper.NormalizeOptionalText(item.Issuer),
                    IssuedOn = item.IssuedOn,
                    ExpiresOn = item.ExpiresOn,
                    CredentialId = TextNormalizationHelper.NormalizeOptionalText(item.CredentialId),
                    CredentialUrl = TextNormalizationHelper.NormalizeOptionalText(item.CredentialUrl)
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .ToList(),
            Languages = aiPreview.Languages
                .Select(item => new CandidateLanguageDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = item.Name.Trim(),
                    Proficiency = string.IsNullOrWhiteSpace(item.Proficiency) ? "Unspecified" : item.Proficiency.Trim()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .DistinctBy(item => item.Name.ToLowerInvariant())
                .ToList(),
            Notes = aiPreview.ParserWarnings.Count > 0
                ? aiPreview.ParserWarnings
                : ["AI parser extracted structured data from the resume. Please verify before saving."],
            ExtractedTextPreview = string.Join(Environment.NewLine, NormalizeResumeText(extractedText).Split('\n').Take(40))
        };
    }

    /// <summary>
    /// Normalizes resume text.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string NormalizeResumeText(string text)
    {
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    /// <summary>
    /// Extracts resume sections.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static Dictionary<string, List<string>> ExtractResumeSections(List<string> lines)
    {
        Dictionary<string, List<string>> sections = new(StringComparer.OrdinalIgnoreCase);
        string currentSection = "general";
        sections[currentSection] = [];

        foreach (string line in lines)
        {
            string? detectedSection = DetectSectionKey(line);
            if (detectedSection != null)
            {
                currentSection = detectedSection;
                if (!sections.ContainsKey(currentSection))
                {
                    sections[currentSection] = [];
                }

                continue;
            }

            sections[currentSection].Add(line);
        }

        return sections;
    }

    /// <summary>
    /// Executes the detect section key operation.
    /// </summary>
    /// <param name="line">The <paramref name="line"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? DetectSectionKey(string line)
    {
        string normalized = line.Trim().Trim(':').ToLowerInvariant();
        if (normalized is "experience" or "work experience" or "employment history" or "professional experience")
        {
            return "experience";
        }

        if (normalized is "projects" or "personal projects" or "project experience")
        {
            return "projects";
        }

        if (normalized is "education" or "academic background")
        {
            return "education";
        }

        if (normalized is "skills" or "technical skills" or "core skills")
        {
            return "skills";
        }

        if (normalized is "certifications" or "licenses" or "awards")
        {
            return "certifications";
        }

        if (normalized is "languages" or "language")
        {
            return "languages";
        }

        if (normalized is "summary" or "profile" or "objective" or "about")
        {
            return "summary";
        }

        return null;
    }

    /// <summary>
    /// Extracts email.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractEmail(string text)
    {
        Match match = EmailExtractorPattern.Match(text);
        return match.Success ? match.Groups["email"].Value.Trim() : null;
    }

    /// <summary>
    /// Extracts phone.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractPhone(string text)
    {
        Match match = PhoneExtractorPattern.Match(text);
        if (!match.Success)
        {
            return null;
        }

        string digits = Regex.Replace(match.Groups["phone"].Value, @"[^\d+]", string.Empty);
        return digits.StartsWith("+84", StringComparison.Ordinal) ? "0" + digits[3..] : digits;
    }

    /// <summary>
    /// Extracts url.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <param name="hostKeyword">The <paramref name="hostKeyword"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractUrl(string text, string hostKeyword)
    {
        Match match = UrlPattern.Matches(text)
            .FirstOrDefault(item => item.Value.Contains(hostKeyword, StringComparison.OrdinalIgnoreCase));
        return match?.Value.Trim();
    }

    /// <summary>
    /// Extracts candidate name.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractCandidateName(List<string> lines)
    {
        return lines
            .Take(6)
            .Select(line => line.Trim(' ', '-', '*', '\t'))
            .FirstOrDefault(line =>
                line.Length >= 4
                && line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 5
                && !line.Contains('@')
                && !line.Contains("linkedin", StringComparison.OrdinalIgnoreCase)
                && !line.Contains("github", StringComparison.OrdinalIgnoreCase)
                && !line.Any(char.IsDigit));
    }

    /// <summary>
    /// Extracts headline.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <param name="name">The <paramref name="name"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractHeadline(List<string> lines, string? name)
    {
        return lines
            .SkipWhile(line => string.Equals(line, name, StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault(line =>
                line.Length >= 4
                && !line.Contains('@')
                && !line.Contains("http", StringComparison.OrdinalIgnoreCase)
                && !YearPattern.IsMatch(line));
    }

    /// <summary>
    /// Extracts location.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractLocation(List<string> lines)
    {
        return lines.FirstOrDefault(line =>
            line.Contains("location", StringComparison.OrdinalIgnoreCase)
            || line.Contains("address", StringComparison.OrdinalIgnoreCase)
            || line.Contains("ho chi minh", StringComparison.OrdinalIgnoreCase)
            || line.Contains("hanoi", StringComparison.OrdinalIgnoreCase)
            || line.Contains("da nang", StringComparison.OrdinalIgnoreCase)
            || line.Contains("vietnam", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts summary.
    /// </summary>
    /// <param name="sections">The <paramref name="sections"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? ExtractSummary(Dictionary<string, List<string>> sections)
    {
        if (!sections.TryGetValue("summary", out List<string>? summaryLines) || summaryLines.Count == 0)
        {
            return null;
        }

        return string.Join(" ", summaryLines.Take(4));
    }

    /// <summary>
    /// Executes the match skills operation.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <param name="allSkills">The <paramref name="allSkills"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<Skill> MatchSkills(string text, IReadOnlyList<Skill> allSkills)
    {
        List<Skill> matches = [];
        foreach (Skill skill in allSkills.OrderByDescending(item => item.Name.Length))
        {
            string escapedSkillName = Regex.Escape(skill.Name);
            if (Regex.IsMatch(text, $@"(?<!\w){escapedSkillName}(?!\w)", RegexOptions.IgnoreCase))
            {
                matches.Add(skill);
            }
        }

        return matches
            .DistinctBy(skill => skill.Id)
            .Take(20)
            .ToList();
    }

    /// <summary>
    /// Extracts years of experience for skill.
    /// </summary>
    /// <param name="text">The <paramref name="text"/> value.</param>
    /// <param name="skillName">The <paramref name="skillName"/> value.</param>
    /// <returns>The operation result.</returns>
    private static decimal? ExtractYearsOfExperienceForSkill(string text, string skillName)
    {
        string escapedSkillName = Regex.Escape(skillName);
        string[] patterns =
        [
            $@"{escapedSkillName}[^\n\.]{{0,32}}?(?<years>\d+(?:\.\d+)?)\s*(?:\+)?\s*(?:years?|yrs?)",
            $@"(?<years>\d+(?:\.\d+)?)\s*(?:\+)?\s*(?:years?|yrs?)[^\n\.]{{0,32}}?{escapedSkillName}",
            $@"{escapedSkillName}\s*[-:()]*\s*(?<years>\d+(?:\.\d+)?)"
        ];

        foreach (string pattern in patterns)
        {
            Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && decimal.TryParse(match.Groups["years"].Value, out decimal parsedYears))
            {
                return parsedYears;
            }
        }

        return null;
    }

    /// <summary>
    /// Executes the split into chunks operation.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<List<string>> SplitIntoChunks(List<string> lines)
    {
        List<List<string>> chunks = [];
        List<string> currentChunk = [];
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentChunk.Count > 0)
                {
                    chunks.Add([.. currentChunk]);
                    currentChunk.Clear();
                }

                continue;
            }

            bool startsNewChunk = currentChunk.Count > 0 && ContainsDateRange(line);
            if (startsNewChunk)
            {
                chunks.Add([.. currentChunk]);
                currentChunk.Clear();
            }

            currentChunk.Add(line.Trim());
        }

        if (currentChunk.Count > 0)
        {
            chunks.Add(currentChunk);
        }

        return chunks;
    }

    /// <summary>
    /// Executes the contains date range operation.
    /// </summary>
    /// <param name="line">The <paramref name="line"/> value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    private static bool ContainsDateRange(string line)
    {
        return Regex.IsMatch(line, @"(?:(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s+)?(?:19|20)\d{2}\s*[-–]\s*(?:(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s+)?(?:(?:19|20)\d{2}|present|current)", RegexOptions.IgnoreCase)
            || Regex.IsMatch(line, @"\b(19|20)\d{2}\b");
    }

    /// <summary>
    /// Parses period from chunk.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CandidateExperiencePeriodDto ParsePeriodFromChunk(IEnumerable<string> lines)
    {
        string combined = string.Join(" ", lines);
        List<int> years = YearPattern.Matches(combined)
            .Select(match => int.TryParse(match.Value, out int year) ? year : 0)
            .Where(year => year > 0)
            .Take(2)
            .ToList();

        int startYear = years.FirstOrDefault();
        int? endYear = years.Skip(1).FirstOrDefault();
        bool isCurrent = Regex.IsMatch(combined, @"\b(present|current|now)\b", RegexOptions.IgnoreCase);

        return new CandidateExperiencePeriodDto
        {
            StartMonth = ExtractMonthNumber(combined) ?? 1,
            StartYear = startYear == 0 ? DateTime.UtcNow.Year : startYear,
            EndMonth = isCurrent ? null : ExtractMonthNumber(combined, last: true),
            EndYear = isCurrent || endYear == 0 ? null : endYear,
            IsCurrent = isCurrent
        };
    }

    /// <summary>
    /// Extracts month number.
    /// </summary>
    /// <param name="value">The <paramref name="value"/> value.</param>
    /// <param name="last">The <paramref name="last"/> value.</param>
    /// <returns>The operation result.</returns>
    private static int? ExtractMonthNumber(string value, bool last = false)
    {
        string[] monthTokens = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "sept", "oct", "nov", "dec"];
        List<int> monthIndexes = [];
        foreach ((string token, int index) in monthTokens.Select((token, index) => (token, index)))
        {
            if (Regex.IsMatch(value, $@"\b{token}[a-z]*\b", RegexOptions.IgnoreCase))
            {
                monthIndexes.Add(index + 1);
            }
        }

        if (monthIndexes.Count == 0)
        {
            return null;
        }

        return last ? monthIndexes.Last() : monthIndexes.First();
    }

    /// <summary>
    /// Parses experience entries.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <param name="fallbackTitle">The <paramref name="fallbackTitle"/> value.</param>
    /// <param name="defaultCompany">The <paramref name="defaultCompany"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CandidateExperienceDto> ParseExperienceEntries(List<string> lines, string fallbackTitle, string defaultCompany)
    {
        return SplitIntoChunks(lines)
            .Select(chunk =>
            {
                CandidateExperiencePeriodDto period = ParsePeriodFromChunk(chunk);
                List<string> contentLines = chunk.Where(line => !ContainsDateRange(line)).ToList();
                string firstLine = contentLines.FirstOrDefault() ?? fallbackTitle;
                string secondLine = contentLines.Skip(1).FirstOrDefault() ?? defaultCompany;
                List<string> bulletLines = contentLines.Skip(2)
                    .Select(line => line.TrimStart('-', '*', '•', ' '))
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();

                if (firstLine.Contains(" at ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = firstLine.Split(" at ", 2, StringSplitOptions.TrimEntries);
                    firstLine = parts[0];
                    secondLine = parts.Length > 1 ? parts[1] : secondLine;
                }

                return new CandidateExperienceDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = firstLine,
                    Company = secondLine,
                    Period = period,
                    Bullets = bulletLines
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Company))
            .Take(8)
            .ToList();
    }

    /// <summary>
    /// Parses projects.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CandidateProjectDto> ParseProjects(List<string> lines)
    {
        return SplitIntoChunks(lines)
            .Select(chunk =>
            {
                CandidateExperiencePeriodDto period = ParsePeriodFromChunk(chunk);
                List<string> contentLines = chunk.Where(line => !ContainsDateRange(line)).ToList();
                string name = contentLines.FirstOrDefault() ?? "Project";
                string? role = contentLines.Skip(1).FirstOrDefault();
                string? description = string.Join(" ", contentLines.Skip(2)).Trim();
                List<string> technologies = Regex.Split(string.Join(" ", chunk), @"[,/|]")
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 1 && value.Length <= 30)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToList();

                return new CandidateProjectDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Role = role,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    Technologies = technologies,
                    Period = period
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Take(6)
            .ToList();
    }

    /// <summary>
    /// Parses educations.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CandidateEducationDto> ParseEducations(List<string> lines)
    {
        return SplitIntoChunks(lines)
            .Select(chunk =>
            {
                string school = chunk.FirstOrDefault() ?? string.Empty;
                string degree = chunk.Skip(1).FirstOrDefault() ?? "Education";
                List<int> years = chunk
                    .SelectMany(line => YearPattern.Matches(line).Select(match => int.Parse(match.Value)))
                    .Take(2)
                    .ToList();

                return new CandidateEducationDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    School = school,
                    Degree = degree,
                    FieldOfStudy = null,
                    StartYear = years.FirstOrDefault() == 0 ? null : years.FirstOrDefault(),
                    EndYear = years.Skip(1).FirstOrDefault() == 0 ? null : years.Skip(1).FirstOrDefault(),
                    Description = chunk.Count > 2 ? string.Join(" ", chunk.Skip(2)) : null
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.School) && !string.IsNullOrWhiteSpace(item.Degree))
            .Take(5)
            .ToList();
    }

    /// <summary>
    /// Parses certifications.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CandidateCertificationDto> ParseCertifications(List<string> lines)
    {
        return SplitIntoChunks(lines)
            .Select(chunk => new CandidateCertificationDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = chunk.FirstOrDefault() ?? string.Empty,
                Issuer = chunk.Skip(1).FirstOrDefault(),
                IssuedOn = null,
                ExpiresOn = null,
                CredentialId = null,
                CredentialUrl = null
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Take(6)
            .ToList();
    }

    /// <summary>
    /// Parses languages.
    /// </summary>
    /// <param name="lines">The <paramref name="lines"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CandidateLanguageDto> ParseLanguages(List<string> lines)
    {
        List<string> proficiencyKeywords = ["native", "fluent", "advanced", "intermediate", "basic", "professional", "business"];
        return lines
            .SelectMany(line => Regex.Split(line, @"[,;|]"))
            .Select(token => token.Trim())
            .Where(token => token.Length > 1)
            .Select(token =>
            {
                string proficiency = proficiencyKeywords
                    .FirstOrDefault(keyword => token.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    ?? "Unspecified";
                string languageName = Regex.Replace(token, @"\((.*?)\)|\b(native|fluent|advanced|intermediate|basic|professional|business)\b", string.Empty, RegexOptions.IgnoreCase).Trim(' ', '-', '–', ':');
                return new CandidateLanguageDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = languageName,
                    Proficiency = proficiency
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .DistinctBy(item => item.Name.ToLowerInvariant())
            .Take(8)
            .ToList();
    }

    private sealed class CandidateExperienceDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public CandidateExperiencePeriodDocument Period { get; set; } = new();
        public List<string> Bullets { get; set; } = [];
    }

    private sealed class CandidateExperiencePeriodDocument
    {
        public int StartMonth { get; set; }
        public int StartYear { get; set; }
        public int? EndMonth { get; set; }
        public int? EndYear { get; set; }
        public bool IsCurrent { get; set; }
    }

    private sealed class CandidateEducationDocument
    {
        public string Id { get; set; } = string.Empty;
        public string School { get; set; } = string.Empty;
        public string Degree { get; set; } = string.Empty;
        public string? FieldOfStudy { get; set; }
        public int? StartYear { get; set; }
        public int? EndYear { get; set; }
        public string? Description { get; set; }
    }

    private sealed class CandidateCertificationDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Issuer { get; set; }
        public DateTime? IssuedOn { get; set; }
        public DateTime? ExpiresOn { get; set; }
        public string? CredentialId { get; set; }
        public string? CredentialUrl { get; set; }
    }

    private sealed class CandidateLanguageDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Proficiency { get; set; } = string.Empty;
    }

    /// <summary>
    /// Builds meta.
    /// </summary>
    /// <param name="page">The <paramref name="page"/> value.</param>
    /// <param name="pageSize">The <paramref name="pageSize"/> value.</param>
    /// <param name="total">The <paramref name="total"/> value.</param>
    /// <returns>The operation result.</returns>
    /// <summary>
    /// Assigns candidate role.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="NotFoundException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    private async Task AssignCandidateRoleAsync(Guid userId)
    {
        Role? candidateRole = await _userRepository.GetRoleByNameAsync(CandidateRoleName);
        if (candidateRole == null)
        {
            throw new NotFoundException("Candidate role not found.");
        }

        await _userRepository.AddUserRoleAsync(new UserRole
        {
            UserId = userId,
            RoleId = candidateRole.Id,
            AssignedAt = DbDateTime.Now
        });
    }

    /// <summary>
    /// Parses and validate import rows.
    /// </summary>
    /// <param name="fileStream">The <paramref name="fileStream"/> value.</param>
    /// <param name="fileName">The <paramref name="fileName"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="ArgumentException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    private async Task<List<CandidateImportPreviewDto>> ParseAndValidateImportRowsAsync(Stream fileStream, string fileName)
    {
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only .xlsx files are supported for candidate import.");
        }

        using MemoryStream buffer = new();
        await fileStream.CopyToAsync(buffer);
        buffer.Position = 0;

        using XLWorkbook workbook = new(buffer);
        IXLWorksheet worksheet = workbook.Worksheets.First();
        List<CandidateImportPreviewDto> rows = [];

        foreach (IXLRow worksheetRow in worksheet.RowsUsed().Skip(1))
        {
            if (worksheetRow.Cells(1, 6).All(cell => string.IsNullOrWhiteSpace(cell.GetString())))
            {
                continue;
            }

            rows.Add(new CandidateImportPreviewDto
            {
                RowNumber = worksheetRow.RowNumber(),
                FullName = worksheetRow.Cell(1).GetString().Trim(),
                Email = worksheetRow.Cell(2).GetString().Trim(),
                PhoneNumber = worksheetRow.Cell(3).GetString().Trim(),
                Source = worksheetRow.Cell(4).GetString().Trim(),
                PositionApplied = worksheetRow.Cell(5).GetString().Trim(),
                Notes = worksheetRow.Cell(6).GetString().Trim()
            });
        }

        await ApplyImportValidationAsync(rows);
        return rows;
    }

    /// <summary>
    /// Validates request rows.
    /// </summary>
    /// <param name="requestRows">The <paramref name="requestRows"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<List<CandidateImportPreviewDto>> ValidateRequestRowsAsync(IEnumerable<CandidateImportRowRequestDto> requestRows)
    {
        List<CandidateImportPreviewDto> rows = requestRows.Select(row => new CandidateImportPreviewDto
        {
            RowNumber = row.RowNumber,
            FullName = row.FullName.Trim(),
            Email = row.Email.Trim(),
            PhoneNumber = row.PhoneNumber.Trim(),
            Source = row.Source.Trim(),
            PositionApplied = row.PositionApplied.Trim(),
            Notes = row.Notes.Trim()
        }).ToList();

        await ApplyImportValidationAsync(rows);
        return rows;
    }

    /// <summary>
    /// Applies import validation.
    /// </summary>
    /// <param name="rows">The <paramref name="rows"/> value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ApplyImportValidationAsync(List<CandidateImportPreviewDto> rows)
    {
        IReadOnlySet<string> existingEmails = await _userRepository.GetExistingEmailsAsync(rows.Select(row => row.Email));
        HashSet<string> duplicateEmailsInFile = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Email))
            .GroupBy(row => row.Email.Trim().ToLowerInvariant())
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

        foreach (CandidateImportPreviewDto row in rows)
        {
            row.Errors.Clear();

            if (string.IsNullOrWhiteSpace(row.FullName))
            {
                row.Errors.Add("Full name is required.");
            }

            if (string.IsNullOrWhiteSpace(row.Email))
            {
                row.Errors.Add("Email is required.");
            }
            else if (!IsValidEmail(row.Email))
            {
                row.Errors.Add("Email format is invalid.");
            }
            else if (existingEmails.Contains(row.Email.Trim().ToLowerInvariant()))
            {
                row.Errors.Add("Email already exists in the database.");
            }
            else if (duplicateEmailsInFile.Contains(row.Email.Trim().ToLowerInvariant()))
            {
                row.Errors.Add("Email is duplicated in the uploaded file.");
            }

            if (!string.IsNullOrWhiteSpace(row.PhoneNumber) && !PhonePattern.IsMatch(row.PhoneNumber))
            {
                row.Errors.Add("Phone number must contain 10 digits and start with 0.");
            }

            row.IsValid = row.Errors.Count == 0;
        }
    }

    /// <summary>
    /// Builds preview response.
    /// </summary>
    /// <param name="rows">The <paramref name="rows"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CandidateImportPreviewResponseDto BuildPreviewResponse(List<CandidateImportPreviewDto> rows)
    {
        return new CandidateImportPreviewResponseDto
        {
            TotalRows = rows.Count,
            ValidRows = rows.Count(row => row.IsValid),
            InvalidRows = rows.Count(row => !row.IsValid),
            Rows = rows
        };
    }

    /// <summary>
    /// Executes the is valid email operation.
    /// </summary>
    /// <param name="email">The <paramref name="email"/> value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates temporary password.
    /// </summary>
    /// <returns>The resulting string value.</returns>
    /// <summary>
    /// Builds imported bio.
    /// </summary>
    /// <param name="source">The <paramref name="source"/> value.</param>
    /// <param name="notes">The <paramref name="notes"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? BuildImportedBio(string source, string notes)
    {
        List<string> parts = [];

        if (!string.IsNullOrWhiteSpace(source))
        {
            parts.Add($"{ImportedSourcePrefix} {source.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            parts.Add($"{ImportedNotesPrefix} {notes.Trim()}");
        }

        return parts.Count == 0 ? null : string.Join(Environment.NewLine, parts);
    }

    /// <summary>
    /// Resolves candidate source.
    /// </summary>
    /// <param name="candidate">The <paramref name="candidate"/> value.</param>
    /// <param name="requestedSource">The <paramref name="requestedSource"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string ResolveCandidateSource(CandidateProfile candidate, string? requestedSource)
    {
        if (!string.IsNullOrWhiteSpace(requestedSource))
        {
            return requestedSource;
        }

        if (!string.IsNullOrWhiteSpace(candidate.Bio) &&
            candidate.Bio.Contains(ImportedSourcePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "BulkImport";
        }

        return "Portal";
    }
}
