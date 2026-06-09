using AutoMapper;
using Microsoft.Extensions.Logging;
using System.Text.Json;
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
    private readonly IMapper _mapper;
    private readonly ILogger<CandidateService> _logger;

    public CandidateService(
        ICandidateProfileRepository candidateRepository,
        IUserRepository userRepository,
        ISkillRepository skillRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        IMapper mapper,
        ILogger<CandidateService> logger)
    {
        _candidateRepository = candidateRepository;
        _userRepository = userRepository;
        _skillRepository = skillRepository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
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
                Source = source ?? "Portal",
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
}
