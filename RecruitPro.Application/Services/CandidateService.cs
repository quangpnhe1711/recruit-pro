using AutoMapper;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
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
    private readonly IMapper _mapper;
    private readonly ILogger<CandidateService> _logger;
    private const string CandidateRoleName = "Candidate";
    private const string ImportedSourcePrefix = "[Imported Source]";
    private const string ImportedNotesPrefix = "[Imported Notes]";
    private const string CandidateLoginUrl = "http://localhost:5173/login";
    private static readonly Regex PhonePattern = new(@"^0\d{9}$", RegexOptions.Compiled);

    public CandidateService(
        ICandidateProfileRepository candidateRepository,
        IUserRepository userRepository,
        ISkillRepository skillRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        IEmailService emailService,
        IMapper mapper,
        ILogger<CandidateService> logger)
    {
        _candidateRepository = candidateRepository;
        _userRepository = userRepository;
        _skillRepository = skillRepository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _emailService = emailService;
        _mapper = mapper;
        _logger = logger;
    }

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
                if (resumeStream != null && !string.IsNullOrWhiteSpace(resumeFileName))
                {
                    uploadedObjectName = $"resumes/{user.Id}/{Guid.NewGuid()}_{Path.GetFileName(resumeFileName)}";
                    resumeObjectKey = await _fileStorage.UploadFileAsync(
                        resumeStream,
                        uploadedObjectName,
                        resumeContentType ?? "application/octet-stream");
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
                    ResumeUrl = resumeObjectKey
                };

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

    public async Task<ApiResponse<HrCandidatesResponseDto>> GetCandidatesAsync(int page, int pageSize, string? keyword, string? status, string? source)
    {
        (IReadOnlyList<CandidateProfile> candidates, int total) = await _candidateRepository.GetPagedAsync(page, pageSize, keyword);

        IEnumerable<HrCandidateListItemDto> items = candidates.Select(candidate =>
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
                Status = latestApplication?.Status.ToString() ?? "New"
            };
        });

        if (!string.IsNullOrWhiteSpace(status))
        {
            items = items.Where(candidate => candidate.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        return ApiResponse<HrCandidatesResponseDto>.Ok(new HrCandidatesResponseDto
        {
            Items = items.ToList(),
            Meta = BuildMeta(page, pageSize, total)
        });
    }

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
            Resume = baseProfile.Resume,
            ApplicationHistory = applicationHistory,
            InterviewHistory = interviewHistory
        });
    }

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

    public async Task<ApiResponse<CandidateImportPreviewResponseDto>> PreviewImportAsync(Stream fileStream, string fileName)
    {
        List<CandidateImportPreviewDto> rows = await ParseAndValidateImportRowsAsync(fileStream, fileName);
        return ApiResponse<CandidateImportPreviewResponseDto>.Ok(BuildPreviewResponse(rows));
    }

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
                string temporaryPassword = GenerateTemporaryPassword();
                User user = new()
                {
                    Id = Guid.NewGuid(),
                    Email = row.Email.Trim(),
                    FullName = row.FullName.Trim(),
                    Phone = NormalizeOptionalText(row.PhoneNumber),
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
                    CurrentPosition = NormalizeOptionalText(row.PositionApplied),
                    Bio = BuildImportedBio(row.Source, row.Notes)
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

    public async Task<ApiResponse<CandidateProfileResponseDto>> GetProfileAsync(Guid userId)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(profile));
    }

    public async Task<ApiResponse<CandidateProfileResponseDto>> UpdateProfileAsync(Guid userId, UpdateCandidateProfileRequest request)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        profile.User.FullName = request.Name ?? profile.User.FullName;
        profile.User.Email = request.Email ?? profile.User.Email;
        profile.User.Phone = request.Phone ?? profile.User.Phone;
        profile.User.UpdatedAt = DbDateTime.Now;
        profile.CurrentPosition = request.Headline ?? profile.CurrentPosition;
        profile.Address = request.Location ?? profile.Address;
        profile.Bio = request.Bio ?? profile.Bio;
        profile.GithubUrl = request.Github ?? profile.GithubUrl;
        profile.LinkedinUrl = request.Linkedin ?? profile.LinkedinUrl;

        await _unitOfWork.BeginTransactionAsync();
        await _candidateRepository.UpdateAsync(profile);
        await _userRepository.UpdateAsync(profile.User);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(profile));
    }

    public async Task<ApiResponse<CandidateProfileResponseDto>> UpdateSkillsAsync(Guid userId, UpdateCandidateSkillsRequest request)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        List<Guid> skillIds = request.SkillIds
            .Select(value => Guid.TryParse(value, out Guid parsed) ? parsed : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToList();
        IReadOnlyList<Skill> skills = await _skillRepository.GetByIdsAsync(skillIds);
        profile.Skills = skills.ToList();

        await _unitOfWork.BeginTransactionAsync();
        await _candidateRepository.UpdateAsync(profile);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(profile));
    }

    public async Task<ApiResponse<CandidateProfileResponseDto>> CreateExperienceAsync(Guid userId, UpsertCandidateExperienceRequest request)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        List<CandidateExperienceDocument> experiences = LoadExperiences(profile);
        experiences.Add(MapExperienceRequest(request, null));
        profile.Education = SerializeExperiences(experiences);

        await SaveProfileAsync(profile);
        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(profile));
    }

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
        profile.Education = SerializeExperiences(experiences);

        await SaveProfileAsync(profile);
        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(profile));
    }

    public async Task<ApiResponse<CandidateProfileResponseDto>> DeleteExperienceAsync(Guid userId, string experienceId)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        List<CandidateExperienceDocument> experiences = LoadExperiences(profile);
        int removedCount = experiences.RemoveAll(item => item.Id.Equals(experienceId, StringComparison.OrdinalIgnoreCase));
        if (removedCount == 0)
        {
            throw new NotFoundException("Experience entry not found.");
        }

        profile.Education = SerializeExperiences(experiences);

        await SaveProfileAsync(profile);
        return ApiResponse<CandidateProfileResponseDto>.Ok(await MapProfileAsync(profile));
    }

    public async Task<ApiResponse<ResumeUploadResponseDto>> UploadResumeAsync(Guid userId, Stream resumeStream, string fileName, string contentType)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        string? previousResumeObjectKey = profile.ResumeUrl;
        DateTime uploadedAt = DbDateTime.Now;
        string objectName = $"resumes/{userId}/{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        string uploadedObjectKey = await _fileStorage.UploadFileAsync(resumeStream, objectName, contentType);
        profile.ResumeUrl = uploadedObjectKey;
        profile.User.UpdatedAt = uploadedAt;

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

        if (!string.IsNullOrWhiteSpace(previousResumeObjectKey)
            && !previousResumeObjectKey.Equals(uploadedObjectKey, StringComparison.Ordinal))
        {
            await _fileStorage.DeleteFileAsync(previousResumeObjectKey);
        }

        _logger.LogInformation("Updated resume for candidate user {UserId} with profile {ProfileId}.", userId, profile.Id);

        return ApiResponse<ResumeUploadResponseDto>.Ok(new ResumeUploadResponseDto
        {
            ResumeId = profile.Id.ToString(),
            FileName = ExtractFileName(uploadedObjectKey),
            UploadedAt = uploadedAt
        });
    }

    public async Task<ApiResponse<ResumeFileResponseDto>> GetResumeDownloadUrlAsync(string resumeId)
    {
        if (!Guid.TryParse(resumeId, out Guid resumeGuid))
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Resume not found.");
        }

        CandidateProfile? profile = await _candidateRepository.GetByIdAsync(resumeGuid);
        if (profile == null || string.IsNullOrWhiteSpace(profile.ResumeUrl))
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Resume not found.");
        }

        string presignedUrl = await _fileStorage.GetPresignedUrlAsync(profile.ResumeUrl);
        return ApiResponse<ResumeFileResponseDto>.Ok(new ResumeFileResponseDto
        {
            ResumeId = profile.Id.ToString(),
            FileName = ExtractFileName(profile.ResumeUrl),
            FileUrl = presignedUrl
        });
    }

    private async Task<CandidateProfile> GetProfileEntityAsync(Guid userId)
    {
        CandidateProfile? profile = await _candidateRepository.GetByUserIdAsync(userId);
        if (profile == null)
        {
            throw new NotFoundException("Candidate profile not found.");
        }

        return profile;
    }

    private async Task<CandidateProfileResponseDto> MapProfileAsync(CandidateProfile profile)
    {
        List<CandidateExperienceDocument> experiences = LoadExperiences(profile);
        CandidateResumeDto? resume = null;

        if (!string.IsNullOrWhiteSpace(profile.ResumeUrl))
        {
            string presignedUrl = await _fileStorage.GetPresignedUrlAsync(profile.ResumeUrl);
            resume = new CandidateResumeDto
            {
                Id = profile.Id.ToString(),
                FileName = ExtractFileName(profile.ResumeUrl),
                FileUrl = presignedUrl,
                UploadedAt = profile.User.UpdatedAt ?? profile.User.CreatedAt ?? DbDateTime.Now
            };
        }

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
                Linkedin = profile.LinkedinUrl
            },
            Skills = profile.Skills.Select(skill => new CandidateSkillViewDto
            {
                Id = skill.Id.ToString(),
                Label = skill.Name,
                Active = true
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
            Resume = resume
        };
    }

    private async Task SaveProfileAsync(CandidateProfile profile)
    {
        await _unitOfWork.BeginTransactionAsync();
        await _candidateRepository.UpdateAsync(profile);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();
    }

    private static List<CandidateExperienceDocument> LoadExperiences(CandidateProfile profile)
    {
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

    private static string? SerializeExperiences(List<CandidateExperienceDocument> experiences)
    {
        return experiences.Count == 0 ? null : JsonSerializer.Serialize(experiences);
    }

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

    private static ApiEnvelopeMeta BuildMeta(int page, int pageSize, int total)
    {
        return new ApiEnvelopeMeta
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    private static string ExtractFileName(string resumeValue)
    {
        if (string.IsNullOrWhiteSpace(resumeValue))
        {
            return string.Empty;
        }

        string fileName = Path.GetFileName(
            Uri.TryCreate(resumeValue, UriKind.Absolute, out Uri? uri)
                ? uri.AbsolutePath
                : resumeValue);

        int separatorIndex = fileName.IndexOf('_');
        return separatorIndex >= 0 && separatorIndex < fileName.Length - 1
            ? fileName[(separatorIndex + 1)..]
            : fileName;
    }

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

    private static string GenerateTemporaryPassword()
    {
        return $"Rp!{Guid.NewGuid():N}"[..12];
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

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
