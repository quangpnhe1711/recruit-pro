using AutoMapper;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Text;
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
    private readonly IResumeParsingService _resumeParsingService;
    private readonly IResumeParsingAiProvider _resumeParsingAiProvider;
    private readonly ISemanticDiscoveryService _semanticDiscoveryService;
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

    // Resume upload hardening: cap the accepted CV at 5 MB and only accept the document formats we can
    // actually parse. The stream is bounded while it is buffered so an oversized upload is rejected
    // before the whole file is pulled into memory.
    private const long MaxResumeFileSizeBytes = 5L * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string[]> AllowedResumeContentTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ["application/pdf"],
            [".docx"] =
            [
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/zip",
                "application/octet-stream"
            ],
            [".txt"] = ["text/plain", "application/octet-stream"]
        };
    private const string ResumeExtractionFailureMessage = "Không đọc được nội dung CV. Hãy dùng file PDF hoặc DOCX có thể chọn văn bản.";
    private const string ResumeAiRetryMessage = "CV đã tải lên, nhưng AI đang bận. Hệ thống sẽ thử lại sau.";
    private const string ResumeAiFailedMessage = "CV đã tải lên, nhưng chưa phân tích được lúc này. Bạn có thể thử lại sau.";
    private const string ResumeProfileMismatchMessage = "CV mới chưa khớp với hồ sơ hiện tại. Hãy phân tích và cập nhật hồ sơ theo CV mới.";
    private static readonly Regex PhonePattern = new(@"^0\d{9}$", RegexOptions.Compiled);
    private static readonly Regex UsernameSanitizerPattern = new(@"[^a-zA-Z0-9._-]", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the CandidateService class.
    /// </summary>
    /// <param name="candidateRepository">The <paramref name="candidateRepository"/> value.</param>
    /// <param name="userRepository">The <paramref name="userRepository"/> value.</param>
    /// <param name="skillRepository">The <paramref name="skillRepository"/> value.</param>
    /// <param name="unitOfWork">The <paramref name="unitOfWork"/> value.</param>
    /// <param name="fileStorage">The <paramref name="fileStorage"/> value.</param>
    /// <param name="emailService">The <paramref name="emailService"/> value.</param>
    /// <param name="resumeParsingService">The <paramref name="resumeParsingService"/> value.</param>
    /// <param name="resumeParsingAiProvider">The <paramref name="resumeParsingAiProvider"/> value.</param>
    /// <param name="semanticDiscoveryService">The <paramref name="semanticDiscoveryService"/> value.</param>
    /// <param name="mapper">The <paramref name="mapper"/> value.</param>
    /// <param name="logger">The <paramref name="logger"/> value.</param>
    public CandidateService(
        ICandidateProfileRepository candidateRepository,
        IUserRepository userRepository,
        ISkillRepository skillRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        IEmailService emailService,
        IResumeParsingService resumeParsingService,
        IResumeParsingAiProvider resumeParsingAiProvider,
        ISemanticDiscoveryService semanticDiscoveryService,
        IMapper mapper,
        ILogger<CandidateService> logger)
    {
        _candidateRepository = candidateRepository;
        _userRepository = userRepository;
        _skillRepository = skillRepository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _emailService = emailService;
        _resumeParsingService = resumeParsingService;
        _resumeParsingAiProvider = resumeParsingAiProvider;
        _semanticDiscoveryService = semanticDiscoveryService;
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
        // Validate and size-bound the optional resume up front (before opening the transaction or
        // touching storage) so a bad/oversized file is rejected without creating a half-built account.
        MemoryStream? bufferedResume = null;
        if (resumeStream != null && !string.IsNullOrWhiteSpace(resumeFileName))
        {
            ResumeBufferResult buffered = await ValidateAndBufferResumeAsync(resumeStream, resumeFileName, resumeContentType);
            if (!buffered.IsValid)
            {
                return ApiResponse<CandidateRegisterResponseDto>.BadRequest(buffered.ErrorCode!);
            }

            bufferedResume = buffered.Buffer;
        }

        await using (bufferedResume)
        {
            return await RegisterInternalAsync(request, bufferedResume, resumeFileName, resumeContentType);
        }
    }

    private async Task<ApiResponse<CandidateRegisterResponseDto>> RegisterInternalAsync(CandidateRegisterRequest request, Stream? resumeStream, string? resumeFileName, string? resumeContentType)
    {
        string? uploadedObjectName = null;
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            User user = new()
            {
                Id = Guid.NewGuid(),
                Username = request.UserInfo.Username.Trim(),
                Email = request.UserInfo.Email,
                FullName = request.UserInfo.FullName,
                Phone = request.UserInfo.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.UserInfo.PasswordHash),
                CreatedAt = DbDateTime.Now,
                UpdatedAt = DbDateTime.Now
            };

            if (await _userRepository.ExistsByUsernameAsync(user.Username))
            {
                return ApiResponse<CandidateRegisterResponseDto>.ValidationError(new[] { new ApiFieldErrorInput { Field = "username", Code = ErrorCodes.UsernameAlreadyExists } });
            }

            if (await _userRepository.ExistsByEmailAsync(user.Email))
            {
                return ApiResponse<CandidateRegisterResponseDto>.ValidationError(new[] { new ApiFieldErrorInput { Field = "email", Code = ErrorCodes.EmailAlreadyExists } });
            }

            await _userRepository.AddAsync(user);
            await AssignCandidateRoleAsync(user.Id);

            CandidateProfile profile;
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
                    CurrentPosition = request.Profile?.CurrentPosition,
                    ExperienceYears = request.Profile?.ExperienceYears,
                    Education = request.Profile?.Education,
                    Address = request.Profile?.Address,
                    Bio = request.Profile?.Bio,
                    GithubUrl = request.Profile?.GitHubUrl,
                    LinkedinUrl = request.Profile?.LinkedInUrl,
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

            return ApiResponse<CandidateRegisterResponseDto>.Created(response, "Tạo tài khoản ứng viên thành công");
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
    public async Task<ApiResponse<HrCandidatesResponseDto>> GetCandidatesAsync(int page, int pageSize, string? keyword, string? status, string? source, Guid? currentUserId = null, IReadOnlyCollection<string>? currentUserRoles = null)
    {
        (IReadOnlyList<CandidateProfile> candidates, int total) = await _candidateRepository.GetPagedAsync(page, pageSize, keyword);

        // Phase 2.2c: SystemAdmin is blocked at [Authorize] and can no longer reach this method.
        // A caller may only see candidates they reach through an application they own.
        IEnumerable<CandidateProfile> visibleCandidates = candidates.Where(candidate =>
            (candidate.User.Applications.Any() || IsProfileComplete(candidate))
            && candidate.User.Applications.Any(application =>
                OwnershipScope.CanAccessApplication(application, currentUserId, currentUserRoles)));

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
    public async Task<ApiResponse<HrCandidateDetailDto>> GetCandidateDetailAsync(string candidateId, Guid? currentUserId = null, IReadOnlyCollection<string>? currentUserRoles = null)
    {
        if (!Guid.TryParse(candidateId, out Guid candidateGuid))
        {
            return ApiResponse<HrCandidateDetailDto>.NotFound(ErrorCodes.CandidateNotFound);
        }

        CandidateProfile? profile = await _candidateRepository.GetHrDetailByIdAsync(candidateGuid);
        if (profile == null)
        {
            return ApiResponse<HrCandidateDetailDto>.NotFound(ErrorCodes.CandidateNotFound);
        }

        // Phase 2.2c: a caller may only open a candidate they reach through an owned application.
        // SystemAdmin is blocked at [Authorize] and can no longer reach this method.
        // Return NotFound (not Forbidden) so an out-of-scope caller cannot probe which candidate ids exist.
        if (!profile.User.Applications.Any(application =>
                OwnershipScope.CanAccessApplication(application, currentUserId, currentUserRoles)))
        {
            return ApiResponse<HrCandidateDetailDto>.NotFound(ErrorCodes.CandidateNotFound);
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
            Sections = baseProfile.Sections,
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
            return ApiResponse<CandidateImportResultDto>.BadRequest(ErrorCodes.InvalidInput);
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
                    Username = await GenerateUniqueUsernameAsync(row.FullName, row.Email),
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
        CandidateProfile profile = await GetOrCreateProfileEntityAsync(userId);
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
            CandidateProfile profile = await GetOrCreateProfileEntityForUpdateAsync(userId);
            await ApplyProfileUpdateAsync(profile, request);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                await TryRefreshCandidateEmbeddingAsync(profile.Id);
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

        throw new InvalidOperationException("Cập nhật hồ sơ ứng viên thất bại sau khi thử lại.");
    }

    public async Task<ApiResponse<CandidateProfileResponseDto>> SaveProfileAsync(
        Guid userId,
        UpdateCandidateProfileRequest request,
        Stream? resumeStream,
        string? resumeFileName,
        string? resumeContentType = null)
    {
        if (resumeStream == null || string.IsNullOrWhiteSpace(resumeFileName))
        {
            return await UpdateProfileAsync(userId, request);
        }

        ResumeBufferResult buffered = await ValidateAndBufferResumeAsync(resumeStream, resumeFileName, resumeContentType);
        if (!buffered.IsValid)
        {
            return ApiResponse<CandidateProfileResponseDto>.BadRequest(buffered.ErrorCode!);
        }

        await using MemoryStream bufferedResume = buffered.Buffer!;
        CandidateProfile profile = await GetOrCreateProfileEntityAsync(userId);
        await ApplyProfileUpdateAsync(profile, request);

        DateTime uploadedAt = DbDateTime.Now;
        string objectName = $"resumes/{userId}/{Guid.NewGuid()}_{Path.GetFileName(resumeFileName)}";
        bufferedResume.Position = 0;
        string uploadedObjectKey = await _fileStorage.UploadFileAsync(
            bufferedResume,
            objectName,
            resumeContentType ?? "application/octet-stream");

        foreach (CandidateResume existingResume in profile.Resumes)
        {
            existingResume.IsCurrent = false;
        }

        int nextVersion = profile.Resumes.Count == 0 ? 1 : profile.Resumes.Max(resume => resume.Version) + 1;
        CandidateResume candidateResume = new()
        {
            CandidateProfileId = profile.Id,
            FileName = Path.GetFileName(resumeFileName),
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

        IReadOnlyList<Skill> allSkills = await _skillRepository.GetAllAsync();
        string extractedText;
        try
        {
            bufferedResume.Position = 0;
            extractedText = _resumeParsingService.NormalizeResumeText(await _resumeParsingService.ExtractResumeTextAsync(bufferedResume, resumeFileName));
        }
        catch (Exception exception) when (exception is not NotSupportedException)
        {
            _logger.LogError(exception, "Resume extraction failed after save for user {UserId} and file {FileName}.", userId, resumeFileName);
            await PersistResumeParsingFailureAsync(profile, ResumeParseStatusTextExtractionFailed, ResumeExtractionFailureMessage, null, []);
            CandidateProfile refreshedProfile = await GetProfileEntityAsync(userId);
            return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(refreshedProfile), ResumeExtractionFailureMessage);
        }

        if (!_resumeParsingService.HasUsableResumeText(extractedText))
        {
            await PersistResumeParsingFailureAsync(profile, ResumeParseStatusTextExtractionFailed, ResumeExtractionFailureMessage, extractedText, []);
            CandidateProfile refreshedProfile = await GetProfileEntityAsync(userId);
            return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(refreshedProfile), ResumeExtractionFailureMessage);
        }

        ResumeParsingAiResult aiResult = await _resumeParsingAiProvider.TryParseResumeAsync(extractedText, allSkills);
        if (aiResult.UsedAi && aiResult.Data != null)
        {
            CandidateResumeParseResponseDto preview = _resumeParsingService.BuildResumeParsePreviewFromAi(aiResult.Data, extractedText, allSkills, aiResult.ModelName);
            await PersistParsedResumeAsync(profile, preview, aiResult.Data, extractedText, aiResult.ModelName);
        }
        else
        {
            CandidateResumeParseResponseDto fallbackPreview = _resumeParsingService.BuildResumeParsePreview(extractedText, allSkills);
            if (aiResult.IsRetryable)
            {
                LogRetryableAiFailure(aiResult);
                await PersistResumeParsingFailureAsync(profile, ResumeParseStatusRetryPending, aiResult.FailureReason, extractedText, fallbackPreview.Notes, aiResult.ModelName);
            }
            else
            {
                await PersistResumeParsingFailureAsync(profile, ResumeParseStatusFailed, aiResult.FailureReason, extractedText, fallbackPreview.Notes, aiResult.ModelName);
            }
        }

        CandidateProfile finalProfile = await GetProfileEntityAsync(userId);
        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(finalProfile));
    }

    /// <summary>
    /// Updates skills.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CandidateProfileResponseDto>> UpdateSkillsAsync(Guid userId, UpdateCandidateSkillsRequest request)
    {
        CandidateProfile profile = await GetOrCreateProfileEntityAsync(userId);
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
        await TryRefreshCandidateEmbeddingAsync(profile.Id);

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
        CandidateProfile profile = await GetOrCreateProfileEntityAsync(userId);
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
            throw new BusinessAppException(ErrorCodes.ExperienceNotFound, 404);
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
            throw new BusinessAppException(ErrorCodes.ExperienceNotFound, 404);
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
        ResumeBufferResult buffered = await ValidateAndBufferResumeAsync(resumeStream, fileName, contentType);
        if (!buffered.IsValid)
        {
            return ApiResponse<CandidateResumeParseResponseDto>.BadRequest(buffered.ErrorCode!);
        }

        await GetProfileEntityAsync(userId);
        await using MemoryStream bufferedResume = buffered.Buffer!;
        string extractedText;
        try
        {
            extractedText = await _resumeParsingService.ExtractResumeTextAsync(bufferedResume, fileName);
        }
        catch (NotSupportedException exception)
        {
            _logger.LogWarning(exception, "Unsupported resume format for parsing: {FileName}", fileName);
            return ApiResponse<CandidateResumeParseResponseDto>.BadRequest(ErrorCodes.ResumeFileUnsupportedType);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to parse resume file {FileName} for user {UserId}.", fileName, userId);
            return ApiResponse<CandidateResumeParseResponseDto>.BadRequest(ErrorCodes.ResumeFileUnsupportedType);
        }

        extractedText = _resumeParsingService.NormalizeResumeText(extractedText);
        if (!_resumeParsingService.HasUsableResumeText(extractedText))
        {
            return ApiResponse<CandidateResumeParseResponseDto>.BadRequest(ErrorCodes.ResumeFileUnsupportedType);
        }

        IReadOnlyList<Skill> allSkills = await _skillRepository.GetAllAsync();
        ResumeParsingAiResult aiResult = await _resumeParsingAiProvider.TryParseResumeAsync(extractedText, allSkills);
        CandidateResumeParseResponseDto preview = aiResult.UsedAi && aiResult.Data != null
            ? _resumeParsingService.BuildResumeParsePreviewFromAi(aiResult.Data, extractedText, allSkills, aiResult.ModelName)
            : _resumeParsingService.BuildResumeParsePreview(extractedText, allSkills);
        preview.ModelName ??= aiResult.ModelName;
        preview.AiFallbackReason = aiResult.UsedAi ? null : aiResult.FailureReason;
        preview.Notes = _resumeParsingService.NormalizeParserWarnings(preview.Notes);
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
        ResumeBufferResult buffered = await ValidateAndBufferResumeAsync(resumeStream, fileName, contentType);
        if (!buffered.IsValid)
        {
            return ApiResponse<ResumeUploadResponseDto>.BadRequest(buffered.ErrorCode!);
        }

        CandidateProfile profile = await GetProfileEntityAsync(userId);
        DateTime uploadedAt = DbDateTime.Now;
        await using MemoryStream bufferedResume = buffered.Buffer!;
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
            extractedText = _resumeParsingService.NormalizeResumeText(await _resumeParsingService.ExtractResumeTextAsync(bufferedResume, fileName));
        }
        catch (Exception exception) when (exception is not NotSupportedException)
        {
            _logger.LogError(exception, "Resume extraction failed after upload for user {UserId} and file {FileName}.", userId, fileName);
            await PersistResumeParsingFailureAsync(profile, ResumeParseStatusTextExtractionFailed, ResumeExtractionFailureMessage, null, []);
            response.ParseStatus = ResumeParseStatusTextExtractionFailed;
            response.ParseMessage = ResumeExtractionFailureMessage;
            return ApiResponse<ResumeUploadResponseDto>.BadRequest(ErrorCodes.ResumeFileUnsupportedType);
        }

        if (!_resumeParsingService.HasUsableResumeText(extractedText))
        {
            await PersistResumeParsingFailureAsync(profile, ResumeParseStatusTextExtractionFailed, ResumeExtractionFailureMessage, extractedText, []);
            response.ParseStatus = ResumeParseStatusTextExtractionFailed;
            response.ParseMessage = ResumeExtractionFailureMessage;
            return ApiResponse<ResumeUploadResponseDto>.BadRequest(ErrorCodes.ResumeFileUnsupportedType);
        }

        ResumeParsingAiResult aiResult = await _resumeParsingAiProvider.TryParseResumeAsync(extractedText, allSkills);
        if (aiResult.UsedAi && aiResult.Data != null)
        {
            CandidateResumeParseResponseDto preview = _resumeParsingService.BuildResumeParsePreviewFromAi(aiResult.Data, extractedText, allSkills, aiResult.ModelName);
            await PersistParsedResumeAsync(profile, preview, aiResult.Data, extractedText, aiResult.ModelName);
            response.ParseStatus = ResumeParseStatusCompleted;
            response.ParseMessage = "Resume uploaded and parsed successfully.";
            response.ParsedAt = profile.ResumeParsedAt;
            response.ParserWarnings = _resumeParsingService.NormalizeParserWarnings(preview.Notes);
            return ApiResponse<ResumeUploadResponseDto>.Ok(response, response.ParseMessage);
        }

        CandidateResumeParseResponseDto fallbackPreview = _resumeParsingService.BuildResumeParsePreview(extractedText, allSkills);
        if (aiResult.IsRetryable)
        {
            LogRetryableAiFailure(aiResult);
            await PersistResumeParsingFailureAsync(profile, ResumeParseStatusRetryPending, aiResult.FailureReason, extractedText, fallbackPreview.Notes, aiResult.ModelName);
            response.ParseStatus = ResumeParseStatusRetryPending;
            response.ParseMessage = ResumeAiRetryMessage;
            response.ParserWarnings = _resumeParsingService.NormalizeParserWarnings(fallbackPreview.Notes);
            return ApiResponse<ResumeUploadResponseDto>.Ok(response, response.ParseMessage);
        }

        await PersistResumeParsingFailureAsync(profile, ResumeParseStatusFailed, aiResult.FailureReason, extractedText, fallbackPreview.Notes, aiResult.ModelName);
        response.ParseStatus = ResumeParseStatusFailed;
        response.ParseMessage = ResumeAiFailedMessage;
        response.ParserWarnings = _resumeParsingService.NormalizeParserWarnings(fallbackPreview.Notes);
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
            return ApiResponse<ResumeFileResponseDto>.NotFound(ErrorCodes.ResumeNotFound);
        }

        CandidateProfile? profile = await _candidateRepository.GetByResumeIdAsync(resumeGuid);
        CandidateResume? resume = profile?.Resumes.FirstOrDefault(item => item.Id == resumeGuid);
        if (profile == null || resume == null)
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound(ErrorCodes.ResumeNotFound);
        }

        string presignedUrl = await _fileStorage.GetPresignedUrlAsync(resume.StorageKey);
        return ApiResponse<ResumeFileResponseDto>.Ok(new ResumeFileResponseDto
        {
            ResumeId = resume.Id.ToString(),
            FileName = resume.FileName,
            FileUrl = presignedUrl
        });
    }

    public async Task<ResumeStreamResponseDto?> GetResumeStreamAsync(string resumeId, Guid requesterId, bool canViewAll)
    {
        if (!Guid.TryParse(resumeId, out Guid resumeGuid))
        {
            return null;
        }

        CandidateProfile? profile = await _candidateRepository.GetByResumeIdAsync(resumeGuid);
        CandidateResume? resume = profile?.Resumes.FirstOrDefault(item => item.Id == resumeGuid);
        if (profile == null || resume == null)
        {
            return null;
        }

        if (!canViewAll && profile.UserId != requesterId)
        {
            return null;
        }

        Stream content = await _fileStorage.DownloadFileAsync(resume.StorageKey);
        return new ResumeStreamResponseDto
        {
            ResumeId = resume.Id.ToString(),
            FileName = resume.FileName,
            ContentType = ResolveResumeContentType(resume.FileName),
            Content = content
        };
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
            throw new BusinessAppException(ErrorCodes.CandidateProfileNotFound, 404);
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
            throw new BusinessAppException(ErrorCodes.CandidateProfileNotFound, 404);
        }

        return profile;
    }

    private async Task<CandidateProfile> GetOrCreateProfileEntityAsync(Guid userId)
    {
        CandidateProfile? profile = await _candidateRepository.GetByUserIdAsync(userId);
        if (profile != null)
        {
            return profile;
        }

        await EnsureCandidateProfileExistsAsync(userId);
        return await GetProfileEntityAsync(userId);
    }

    private async Task<CandidateProfile> GetOrCreateProfileEntityForUpdateAsync(Guid userId)
    {
        CandidateProfile? profile = await _candidateRepository.GetByUserIdForUpdateAsync(userId);
        if (profile != null)
        {
            return profile;
        }

        await EnsureCandidateProfileExistsAsync(userId);
        return await GetProfileEntityForUpdateAsync(userId);
    }

    private async Task EnsureCandidateProfileExistsAsync(Guid userId)
    {
        CandidateProfile? existingProfile = await _candidateRepository.GetByUserIdForUpdateAsync(userId);
        if (existingProfile != null)
        {
            return;
        }

        User? user = await _userRepository.GetTrackedByIdAsync(userId);
        if (user == null)
        {
            throw new BusinessAppException(ErrorCodes.UserNotFound, 404);
        }

        CandidateProfile profile = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            ResumeParseStatus = ResumeParseStatusNotStarted,
            CandidateEmbeddingStatus = ResumeEmbeddingStatusNotStarted
        };

        user.CandidateProfile = profile;
        await _candidateRepository.SaveAsync(profile);
        await _unitOfWork.SaveChangesAsync();
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
        IReadOnlyList<CandidateSectionSnapshot> sections = CandidateProfileSectionHelper.BuildSections(profile);
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
                FileUrl = string.Empty,
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
                Username = profile.User.Username,
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
            Sections = sections.Select(MapSectionSnapshot).ToList(),
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
        await TryRefreshCandidateEmbeddingAsync(profile.Id);
    }

    /// <summary>
    /// Applies profile update.
    /// </summary>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ApplyProfileUpdateAsync(CandidateProfile profile, UpdateCandidateProfileRequest request)
    {
        string nextEmail = request.Email ?? profile.User.Email;

        if (!string.Equals(nextEmail, profile.User.Email, StringComparison.OrdinalIgnoreCase)
            && await _userRepository.ExistsByEmailAsync(nextEmail, profile.User.Id))
        {
            throw new BusinessAppException(ErrorCodes.ValidationFailed, 400, fieldErrors: new[] { new ApiFieldErrorInput { Field = "email", Code = ErrorCodes.EmailAlreadyExists } });
        }

        profile.User.FullName = request.Name ?? profile.User.FullName;
        profile.User.Email = nextEmail;
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

        if (request.Sections != null)
        {
            await ReplaceCandidateSectionsAsync(profile.Id, request.Sections);
        }
        else
        {
            await SyncLegacySectionsAsync(profile);
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

    private static CandidateProfileSectionDto MapSectionSnapshot(CandidateSectionSnapshot section)
    {
        return new CandidateProfileSectionDto
        {
            Id = section.Id == Guid.Empty ? string.Empty : section.Id.ToString(),
            SectionKey = section.SectionKey,
            Title = section.Title,
            SectionType = section.SectionType,
            Source = section.Source,
            DisplayOrder = section.DisplayOrder,
            Schema = DeserializeDictionary(section.SchemaJson),
            Items = section.Items.Select(item => new CandidateProfileSectionItemDto
            {
                Id = item.Id == Guid.Empty ? string.Empty : item.Id.ToString(),
                ItemType = item.ItemType,
                Title = item.Title,
                Subtitle = item.Subtitle,
                Organization = item.Organization,
                Location = item.Location,
                Description = item.Description,
                DateLabel = item.DateLabel,
                StartMonth = item.StartMonth,
                StartYear = item.StartYear,
                EndMonth = item.EndMonth,
                EndYear = item.EndYear,
                IsCurrent = item.IsCurrent,
                DisplayOrder = item.DisplayOrder,
                Tags = item.Tags,
                Attributes = item.Attributes
            }).ToList()
        };
    }

    private List<CandidateProfileSection> BuildDefaultSectionsFromLegacyProfile(CandidateProfile profile)
    {
        return CandidateProfileSectionHelper.BuildSections(profile)
            .Where(section => string.IsNullOrWhiteSpace(section.Source) || !section.Source.Equals("User", StringComparison.OrdinalIgnoreCase))
            .Select(MapSectionSnapshotToEntity)
            .ToList();
    }

    private static CandidateProfileSection MapSectionRequest(CandidateProfileSectionUpsertRequest request)
    {
        return new CandidateProfileSection
        {
            Id = Guid.TryParse(request.Id, out Guid sectionId) ? sectionId : Guid.NewGuid(),
            SectionKey = TextNormalizationHelper.NormalizeOptionalText(request.SectionKey),
            Title = request.Title.Trim(),
            SectionType = string.IsNullOrWhiteSpace(request.SectionType) ? "Custom" : request.SectionType.Trim(),
            Source = string.IsNullOrWhiteSpace(request.Source) ? "User" : request.Source.Trim(),
            DisplayOrder = request.DisplayOrder,
            SchemaJson = SerializeDictionary(request.Schema),
            Items = request.Items
                .OrderBy(item => item.DisplayOrder)
                .Select(MapSectionItemRequest)
                .ToList(),
            UpdatedAt = DbDateTime.Now
        };
    }

    private static CandidateProfileSectionItem MapSectionItemRequest(CandidateProfileSectionItemUpsertRequest request)
    {
        return new CandidateProfileSectionItem
        {
            Id = Guid.TryParse(request.Id, out Guid itemId) ? itemId : Guid.NewGuid(),
            ItemType = string.IsNullOrWhiteSpace(request.ItemType) ? "Entry" : request.ItemType.Trim(),
            Title = request.Title.Trim(),
            Subtitle = TextNormalizationHelper.NormalizeOptionalText(request.Subtitle),
            Organization = TextNormalizationHelper.NormalizeOptionalText(request.Organization),
            Location = TextNormalizationHelper.NormalizeOptionalText(request.Location),
            Description = TextNormalizationHelper.NormalizeOptionalText(request.Description),
            DateLabel = TextNormalizationHelper.NormalizeOptionalText(request.DateLabel),
            StartMonth = request.StartMonth,
            StartYear = request.StartYear,
            EndMonth = request.IsCurrent ? null : request.EndMonth,
            EndYear = request.IsCurrent ? null : request.EndYear,
            IsCurrent = request.IsCurrent,
            DisplayOrder = request.DisplayOrder,
            TagsJson = SerializeStringList(request.Tags),
            AttributesJson = SerializeDictionary(request.Attributes),
            UpdatedAt = DbDateTime.Now
        };
    }

    private static CandidateProfileSection MapSectionSnapshotToEntity(CandidateSectionSnapshot snapshot)
    {
        return new CandidateProfileSection
        {
            Id = snapshot.Id == Guid.Empty ? Guid.NewGuid() : snapshot.Id,
            SectionKey = snapshot.SectionKey,
            Title = snapshot.Title,
            SectionType = snapshot.SectionType,
            Source = string.IsNullOrWhiteSpace(snapshot.Source) ? "System" : snapshot.Source,
            DisplayOrder = snapshot.DisplayOrder,
            SchemaJson = snapshot.SchemaJson,
            CreatedAt = DbDateTime.Now,
            UpdatedAt = DbDateTime.Now,
            Items = snapshot.Items.Select(item => new CandidateProfileSectionItem
            {
                Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                ItemType = item.ItemType,
                Title = item.Title,
                Subtitle = item.Subtitle,
                Organization = item.Organization,
                Location = item.Location,
                Description = item.Description,
                DateLabel = item.DateLabel,
                StartMonth = item.StartMonth,
                StartYear = item.StartYear,
                EndMonth = item.IsCurrent ? null : item.EndMonth,
                EndYear = item.IsCurrent ? null : item.EndYear,
                IsCurrent = item.IsCurrent,
                DisplayOrder = item.DisplayOrder,
                TagsJson = SerializeStringList(item.Tags),
                AttributesJson = SerializeDictionary(item.Attributes),
                CreatedAt = DbDateTime.Now,
                UpdatedAt = DbDateTime.Now
            }).ToList()
        };
    }

    private static CandidateProfileSection CloneSection(CandidateProfileSection section)
    {
        return new CandidateProfileSection
        {
            Id = section.Id == Guid.Empty ? Guid.NewGuid() : section.Id,
            CandidateProfileId = section.CandidateProfileId,
            SectionKey = section.SectionKey,
            Title = section.Title,
            SectionType = section.SectionType,
            Source = section.Source,
            DisplayOrder = section.DisplayOrder,
            SchemaJson = section.SchemaJson,
            CreatedAt = section.CreatedAt ?? DbDateTime.Now,
            UpdatedAt = DbDateTime.Now,
            Items = section.Items
                .OrderBy(item => item.DisplayOrder)
                .Select(item => new CandidateProfileSectionItem
                {
                    Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                    ItemType = item.ItemType,
                    Title = item.Title,
                    Subtitle = item.Subtitle,
                    Organization = item.Organization,
                    Location = item.Location,
                    Description = item.Description,
                    DateLabel = item.DateLabel,
                    StartMonth = item.StartMonth,
                    StartYear = item.StartYear,
                    EndMonth = item.EndMonth,
                    EndYear = item.EndYear,
                    IsCurrent = item.IsCurrent,
                    DisplayOrder = item.DisplayOrder,
                    TagsJson = item.TagsJson,
                    AttributesJson = item.AttributesJson,
                    CreatedAt = item.CreatedAt ?? DbDateTime.Now,
                    UpdatedAt = DbDateTime.Now
                })
                .ToList()
        };
    }

    private static CandidateProfileSection MapSectionDtoToEntity(CandidateProfileSectionDto section)
    {
        return new CandidateProfileSection
        {
            Id = Guid.TryParse(section.Id, out Guid sectionId) ? sectionId : Guid.NewGuid(),
            SectionKey = section.SectionKey,
            Title = section.Title,
            SectionType = section.SectionType,
            Source = section.Source,
            DisplayOrder = section.DisplayOrder,
            SchemaJson = SerializeDictionary(section.Schema),
            CreatedAt = DbDateTime.Now,
            UpdatedAt = DbDateTime.Now,
            Items = section.Items
                .OrderBy(item => item.DisplayOrder)
                .Select(item => new CandidateProfileSectionItem
                {
                    Id = Guid.TryParse(item.Id, out Guid itemId) ? itemId : Guid.NewGuid(),
                    ItemType = item.ItemType,
                    Title = item.Title,
                    Subtitle = item.Subtitle,
                    Organization = item.Organization,
                    Location = item.Location,
                    Description = item.Description,
                    DateLabel = item.DateLabel,
                    StartMonth = item.StartMonth,
                    StartYear = item.StartYear,
                    EndMonth = item.IsCurrent ? null : item.EndMonth,
                    EndYear = item.IsCurrent ? null : item.EndYear,
                    IsCurrent = item.IsCurrent,
                    DisplayOrder = item.DisplayOrder,
                    TagsJson = SerializeStringList(item.Tags),
                    AttributesJson = SerializeDictionary(item.Attributes),
                    CreatedAt = DbDateTime.Now,
                    UpdatedAt = DbDateTime.Now
                })
                .ToList()
        };
    }

    private static bool IsManagedLegacySectionKey(string? sectionKey)
    {
        return sectionKey?.Trim().ToLowerInvariant() switch
        {
            "experience" => true,
            "projects" => true,
            "education" => true,
            "certifications" => true,
            "languages" => true,
            _ => false
        };
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
            profile.ParsedResumeJson = JsonSerializer.Serialize(rawAiData);
            profile.ResumeExtractedText = extractedText;
            profile.ResumeParseStatus = ResumeParseStatusCompleted;
            profile.ResumeParseError = null;
            profile.ResumeParseModel = modelName;
            profile.ResumeParserWarningsJson = SerializeDocuments(_resumeParsingService.NormalizeParserWarnings(preview.Notes));
            profile.ResumeParsedAt = DbDateTime.Now;
            profile.CandidateEmbeddingStatus ??= ResumeEmbeddingStatusNotStarted;
            await _candidateRepository.UpdateAsync(profile);
            await _userRepository.UpdateAsync(profile.User);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            await TryRefreshCandidateEmbeddingAsync(profile.Id);
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
            profile.ResumeParserWarningsJson = SerializeDocuments(_resumeParsingService.NormalizeParserWarnings(warnings));
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

    private async Task TryRefreshCandidateEmbeddingAsync(Guid candidateProfileId)
    {
        try
        {
            await _semanticDiscoveryService.RefreshCandidateEmbeddingAsync(candidateProfileId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Candidate embedding refresh failed after profile update for candidate profile {CandidateProfileId}.",
                candidateProfileId);
        }
    }

    private async Task ApplyParsedResumeToProfileAsync(CandidateProfile profile, CandidateResumeParseResponseDto preview)
    {
        profile.User.FullName = string.IsNullOrWhiteSpace(preview.Profile.Name) ? profile.User.FullName : TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Name) ?? profile.User.FullName;
        if (!string.IsNullOrWhiteSpace(preview.Profile.Email))
        {
            string nextEmail = TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Email) ?? profile.User.Email;
            if (!string.Equals(nextEmail, profile.User.Email, StringComparison.OrdinalIgnoreCase)
                && !await _userRepository.ExistsByEmailAsync(nextEmail, profile.User.Id))
            {
                profile.User.Email = nextEmail;
            }
        }
        profile.User.Phone = string.IsNullOrWhiteSpace(preview.Profile.Phone) ? string.Empty : TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Phone) ?? string.Empty;
        profile.User.UpdatedAt = DbDateTime.Now;
        profile.CurrentPosition = TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Headline);
        profile.Address = TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Location);
        profile.Bio = TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Bio);
        profile.GithubUrl = TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Github);
        profile.LinkedinUrl = TextNormalizationHelper.NormalizeOptionalText(preview.Profile.Linkedin);
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

        if (preview.Sections.Count > 0)
        {
            await _candidateRepository.ReplaceSectionsAsync(
                profile.Id,
                preview.Sections.Select(MapSectionDtoToEntity).ToList());
        }
        else
        {
            await SyncLegacySectionsAsync(profile);
        }
    }

    private async Task ReplaceCandidateSectionsAsync(
        Guid candidateProfileId,
        IEnumerable<CandidateProfileSectionUpsertRequest> requestedSections)
    {
        List<CandidateProfileSection> sections = requestedSections
            .OrderBy(section => section.DisplayOrder)
            .Select(MapSectionRequest)
            .ToList();

        await _candidateRepository.ReplaceSectionsAsync(candidateProfileId, sections);
    }

    private async Task SyncLegacySectionsAsync(CandidateProfile profile)
    {
        List<CandidateProfileSection> sections = BuildDefaultSectionsFromLegacyProfile(profile);
        if (profile.Sections.Count > 0)
        {
            List<CandidateProfileSection> customSections = profile.Sections
                .Where(section => string.IsNullOrWhiteSpace(section.SectionKey) || !IsManagedLegacySectionKey(section.SectionKey))
                .Select(CloneSection)
                .ToList();

            sections.AddRange(customSections);
            sections = sections
                .OrderBy(section => section.DisplayOrder)
                .ToList();
        }

        await _candidateRepository.ReplaceSectionsAsync(profile.Id, sections);
        profile.Sections = sections;
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

    /// <summary>
    /// Outcome of validating and buffering an uploaded resume. On success <see cref="Buffer"/> holds the
    /// fully-buffered (and size-bounded) file positioned at 0; on failure it carries a stable error code
    /// and a user-facing message and <see cref="Buffer"/> is null.
    /// </summary>
    private readonly record struct ResumeBufferResult(MemoryStream? Buffer, string? ErrorMessage, string? ErrorCode)
    {
        public bool IsValid => Buffer != null;

        public static ResumeBufferResult Fail(string message, string errorCode) => new(null, message, errorCode);
        public static ResumeBufferResult Success(MemoryStream buffer) => new(buffer, null, null);
    }

    /// <summary>
    /// Validates an uploaded resume (presence, extension, MIME type) and streams it into memory with a
    /// hard size cap. The copy is chunked so an oversized file is rejected mid-stream instead of being
    /// fully materialised, and an empty file is rejected. The returned buffer is owned by the caller.
    /// </summary>
    private static async Task<ResumeBufferResult> ValidateAndBufferResumeAsync(Stream? source, string? fileName, string? contentType)
    {
        if (source == null || string.IsNullOrWhiteSpace(fileName))
        {
            return ResumeBufferResult.Fail("Vui lòng chọn một tệp CV hợp lệ.", ErrorCodes.ResumeFileRequired);
        }

        string extension = Path.GetExtension(fileName).Trim().ToLowerInvariant();
        if (!AllowedResumeContentTypes.TryGetValue(extension, out string[]? allowedTypes))
        {
            return ResumeBufferResult.Fail(
                "Định dạng CV không hỗ trợ. Hãy tải lên PDF, DOCX hoặc TXT.", ErrorCodes.ResumeFileUnsupportedType);
        }

        // The browser-supplied content type is advisory (clients vary), but a clearly wrong MIME type
        // (image, executable, ...) for the extension is rejected. octet-stream is tolerated for docx/txt.
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            string normalizedType = contentType.Split(';')[0].Trim();
            if (!allowedTypes.Contains(normalizedType, StringComparer.OrdinalIgnoreCase))
            {
                return ResumeBufferResult.Fail(
                    "Loại nội dung tệp CV không hợp lệ.", ErrorCodes.ResumeFileUnsupportedType);
            }
        }

        MemoryStream buffer = new();
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        byte[] chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk.AsMemory(0, chunk.Length))) > 0)
        {
            total += read;
            if (total > MaxResumeFileSizeBytes)
            {
                await buffer.DisposeAsync();
                return ResumeBufferResult.Fail(
                    "Tệp CV vượt quá dung lượng tối đa 5MB.", ErrorCodes.ResumeFileTooLarge);
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read));
        }

        if (total == 0)
        {
            await buffer.DisposeAsync();
            return ResumeBufferResult.Fail("Tệp CV rỗng.", ErrorCodes.ResumeFileEmpty);
        }

        buffer.Position = 0;
        return ResumeBufferResult.Success(buffer);
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

    private static string? SerializeStringList(IEnumerable<string>? values)
    {
        if (values == null)
        {
            return null;
        }

        List<string> normalized = values
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList();

        return normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized);
    }

    private static string? SerializeDictionary(Dictionary<string, string>? values)
    {
        if (values == null || values.Count == 0)
        {
            return null;
        }

        Dictionary<string, string> normalized = values
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key.Trim(), item => item.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        return normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized);
    }

    private static Dictionary<string, string> DeserializeDictionary(string? jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString) ?? [];
        }
        catch
        {
            return [];
        }
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
            FileUrl = $"/api/resumes/{candidateResume.Id}/preview",
            UploadedAt = candidateResume.UploadDate,
            Version = candidateResume.Version,
            IsCurrent = candidateResume.IsCurrent
        };
    }

    private static string ResolveResumeContentType(string fileName)
    {
        return Path.GetExtension(fileName).Trim().ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
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
            throw new BusinessAppException(ErrorCodes.CandidateRoleNotFound, 404);
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
            throw new ArgumentException("Chỉ hỗ trợ file .xlsx để import ứng viên.");
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
                row.Errors.Add("Họ tên là bắt buộc.");
            }

            if (string.IsNullOrWhiteSpace(row.Email))
            {
                row.Errors.Add("Email là bắt buộc.");
            }
            else if (!IsValidEmail(row.Email))
            {
                row.Errors.Add("Email không đúng định dạng.");
            }
            else if (existingEmails.Contains(row.Email.Trim().ToLowerInvariant()))
            {
                row.Errors.Add("Email đã tồn tại.");
            }
            else if (duplicateEmailsInFile.Contains(row.Email.Trim().ToLowerInvariant()))
            {
                row.Errors.Add("Email bị trùng trong file tải lên.");
            }

            if (!string.IsNullOrWhiteSpace(row.PhoneNumber) && !PhonePattern.IsMatch(row.PhoneNumber))
            {
                row.Errors.Add("Số điện thoại phải có 10 số và bắt đầu bằng 0.");
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

    private async Task<string> GenerateUniqueUsernameAsync(string fullName, string email)
    {
        string baseUsername = BuildUsernameBase(fullName, email);
        string candidate = baseUsername;
        int suffix = 1;

        while (await _userRepository.ExistsByUsernameAsync(candidate))
        {
            suffix++;
            candidate = $"{baseUsername}-{suffix}";
        }

        return candidate;
    }

    private static string BuildUsernameBase(string fullName, string email)
    {
        string preferred = TextNormalizationHelper.NormalizeOptionalText(fullName)?.Trim() ?? string.Empty;
        preferred = preferred.Length == 0 ? email.Split('@')[0] : preferred.Replace(' ', '-').ToLowerInvariant();
        preferred = UsernameSanitizerPattern.Replace(preferred, "-").Trim('-', '.');

        if (string.IsNullOrWhiteSpace(preferred))
        {
            preferred = $"user-{Guid.NewGuid():N}"[..13];
        }

        if (preferred.Length < 4)
        {
            preferred = $"{preferred}-user";
        }

        return preferred[..Math.Min(preferred.Length, 40)];
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
