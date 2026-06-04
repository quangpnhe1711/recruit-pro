using AutoMapper;
using RecruitPro.Application.DTOs.Request.Candidate;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Application.Interfaces.IRepositories;

namespace RecruitPro.Application.Services
{
    public class CandidateProfileService : ICandidateProfileService
    {
        private readonly ICandidateProfileRepository _candidateRepository;
        private readonly IUserRepository _userRepository;
        private readonly IJobRepository _jobRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly IMapper _mapper;

        public CandidateProfileService(
            ICandidateProfileRepository candidateRepository,
            IUserRepository userRepository,
            IJobRepository jobRepository,
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorage,
            IMapper mapper)
        {
            _candidateRepository = candidateRepository;
            _userRepository = userRepository;
            _jobRepository = jobRepository;
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _mapper = mapper;
        }

        public async Task<ApiResponse<CandidateRegisterResponseDto>> RegisterAsync(CandidateRegisterRequest request, Stream? resumeStream, string? resumeFileName, string? resumeContentType = null)
        {
            string? uploadedObjectName = null;
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = request.UserInfo.Email,
                    FullName = request.UserInfo.FullName,
                    Phone = request.UserInfo.Phone,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.UserInfo.PasswordHash),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _userRepository.AddAsync(user);

                CandidateProfile? profile = null;
                if (request.Profile != null)
                {
                    string? resumeUrl = null;
                    if (resumeStream != null && !string.IsNullOrWhiteSpace(resumeFileName))
                    {
                        uploadedObjectName = $"resumes/{user.Id}/{Guid.NewGuid()}_{Path.GetFileName(resumeFileName)}";
                        resumeUrl = await _fileStorage.UploadFileAsync(
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
                        ResumeUrl = resumeUrl
                    };

                    await _candidateRepository.SaveAsync(profile);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                var response = _mapper.Map<CandidateRegisterResponseDto>(user, options =>
                {
                    options.Items["CandidateProfileId"] = profile?.Id ?? Guid.Empty;
                });

                return ApiResponse<CandidateRegisterResponseDto>.Created(response, "Candidate registered successfully");
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                if (!string.IsNullOrWhiteSpace(uploadedObjectName))
                {
                    try
                    {
                        await _fileStorage.DeleteFileAsync(uploadedObjectName);
                    }
                    catch
                    {
                    }
                }

                throw;
            }
        }

        public async Task<ApiResponse<CandidateDashboardDto>> GetDashboardAsync(Guid userId)
        {
            var profile = await GetProfileEntityAsync(userId);
            var allCandidateApplications = await _jobRepository.GetApplicationsByUserIdAsync(profile.UserId);
            var recommended = (await _jobRepository.SearchApprovedAsync(profile.CurrentPosition, [], profile.Skills.Select(x => x.Name).ToList(), "newest", 1, 5)).Jobs;

            var upcomingInterview = allCandidateApplications
                .SelectMany(app => app.Interviews.Select(interview => new { app, interview }))
                .Where(x => x.interview.InterviewDate >= DateTime.UtcNow)
                .OrderBy(x => x.interview.InterviewDate)
                .FirstOrDefault();

            var response = new CandidateDashboardDto
            {
                GreetingName = profile.User.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? profile.User.FullName,
                Stats = new CandidateDashboardStatsDto
                {
                    AppliedJobs = allCandidateApplications.Count,
                    Interviews = allCandidateApplications.SelectMany(x => x.Interviews).Count(),
                    UnreadNotifications = 0
                },
                UpcomingInterview = upcomingInterview == null ? null : new UpcomingInterviewDto
                {
                    Id = upcomingInterview.interview.Id.ToString(),
                    Date = DateOnly.FromDateTime(upcomingInterview.interview.InterviewDate).ToString("yyyy-MM-dd"),
                    Time = upcomingInterview.interview.InterviewDate.ToString("HH:mm"),
                    JobTitle = upcomingInterview.app.Job.Title,
                    InterviewerName = "RecruitPro Interviewer",
                    InterviewerTitle = "HR",
                    MeetingUrl = upcomingInterview.interview.MeetingLink
                },
                RecommendedJobs = recommended.Select(job => new RecommendedJobDto
                {
                    Id = job.Id.ToString(),
                    Title = job.Title,
                    Meta = $"{job.WorkMode} • {(job.SalaryMin.HasValue || job.SalaryMax.HasValue ? $"{job.SalaryMin:0}-{job.SalaryMax:0}" : "Negotiable")}",
                    EmploymentType = job.EmploymentType.ToString(),
                    Skills = job.JobSkills.Select(x => x.Skill.Name).Distinct().ToList()
                }).ToList()
            };

            return ApiResponse<CandidateDashboardDto>.Ok(response);
        }

        public async Task<ApiResponse<CandidateApplicationsResponseDto>> GetApplicationsAsync(Guid userId, int page, int pageSize, string? status, string? keyword)
        {
            var profile = await GetProfileEntityAsync(userId);
            IEnumerable<RecruitPro.Domain.Entities.Application> query = await _jobRepository.GetApplicationsByUserIdAsync(profile.UserId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => x.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var loweredKeyword = keyword.Trim().ToLowerInvariant();
                query = query.Where(x => x.Job.Title.ToLower().Contains(loweredKeyword));
            }

            var total = query.Count();
            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(app => new CandidateApplicationListItemDto
                {
                    Id = app.Id.ToString(),
                    JobId = app.JobId.ToString(),
                    JobTitle = app.Job.Title,
                    CompanyOrDepartment = app.Job.Department != null ? app.Job.Department.Name : "RecruitPro",
                    AppliedDate = app.AppliedAt,
                    Status = app.Status.ToString(),
                    NextStep = app.Interviews.Any() ? "Upcoming interview" : "Awaiting review",
                    AvailableActions = app.Status == ApplicationStatus.Accepted
                        ? new List<string> { "viewDetail" }
                        : new List<string> { "viewDetail", "withdraw" }
                }).ToList();

            var response = new CandidateApplicationsResponseDto
            {
                Items = items,
                Meta = BuildMeta(page, pageSize, total),
                Summary = new CandidateApplicationSummaryDto
                {
                    Total = total,
                    Active = query.Count(x => x.Status != ApplicationStatus.Rejected && x.Status != ApplicationStatus.Accepted),
                    Closed = query.Count(x => x.Status == ApplicationStatus.Rejected || x.Status == ApplicationStatus.Accepted)
                }
            };

            return ApiResponse<CandidateApplicationsResponseDto>.Ok(response);
        }

        public async Task<ApiResponse<string>> WithdrawApplicationAsync(Guid userId, string applicationId)
        {
            var profile = await GetProfileEntityAsync(userId);
            if (!Guid.TryParse(applicationId, out var applicationGuid))
            {
                throw new NotFoundException("Application not found.");
            }

            var application = (await _jobRepository.GetApplicationsByUserIdAsync(profile.UserId)).FirstOrDefault(x => x.Id == applicationGuid);
            if (application == null)
            {
                throw new NotFoundException("Application not found.");
            }

            application.Status = ApplicationStatus.Rejected;
            await _unitOfWork.BeginTransactionAsync();
            await _jobRepository.UpdateApplicationAsync(application);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return ApiResponse<string>.Ok("Application withdrawn successfully", "Application withdrawn successfully");
        }

        public async Task<ApiResponse<CandidateProfileResponseDto>> GetProfileAsync(Guid userId)
        {
            var profile = await GetProfileEntityAsync(userId);
            return ApiResponse<CandidateProfileResponseDto>.Ok(MapProfile(profile));
        }

        public async Task<ApiResponse<CandidateProfileResponseDto>> UpdateProfileAsync(Guid userId, UpdateCandidateProfileRequest request)
        {
            var profile = await GetProfileEntityAsync(userId);
            profile.User.FullName = request.Name ?? profile.User.FullName;
            profile.User.Email = request.Email ?? profile.User.Email;
            profile.User.Phone = request.Phone ?? profile.User.Phone;
            profile.User.UpdatedAt = DateTime.UtcNow;
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

            return ApiResponse<CandidateProfileResponseDto>.Ok(MapProfile(profile));
        }

        public async Task<ApiResponse<CandidateProfileResponseDto>> UpdateSkillsAsync(Guid userId, UpdateCandidateSkillsRequest request)
        {
            var profile = await GetProfileEntityAsync(userId);
            profile.Skills = profile.Skills.Where(skill => request.SkillIds.Contains(skill.Id.ToString(), StringComparer.OrdinalIgnoreCase)).ToList();

            await _unitOfWork.BeginTransactionAsync();
            await _candidateRepository.UpdateAsync(profile);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            return ApiResponse<CandidateProfileResponseDto>.Ok(MapProfile(profile));
        }

        public async Task<ApiResponse<ResumeUploadResponseDto>> UploadResumeAsync(Guid userId, Stream resumeStream, string fileName, string contentType)
        {
            var profile = await GetProfileEntityAsync(userId);
            var objectName = $"resumes/{userId}/{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
            var fileUrl = await _fileStorage.UploadFileAsync(resumeStream, objectName, contentType);
            profile.ResumeUrl = fileUrl;

            await _unitOfWork.BeginTransactionAsync();
            await _candidateRepository.UpdateAsync(profile);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            return ApiResponse<ResumeUploadResponseDto>.Ok(new ResumeUploadResponseDto
            {
                ResumeId = profile.Id.ToString(),
                FileName = fileName,
                UploadedAt = DateTime.UtcNow
            });
        }

        private async Task<CandidateProfile> GetProfileEntityAsync(Guid userId)
        {
            var profile = await _candidateRepository.GetByUserIdAsync(userId);
            if (profile == null)
            {
                throw new NotFoundException("Candidate profile not found.");
            }

            return profile;
        }

        private static CandidateProfileResponseDto MapProfile(CandidateProfile profile)
        {
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
                    MemberSince = (profile.User.CreatedAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd"),
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
                Resume = string.IsNullOrWhiteSpace(profile.ResumeUrl)
                    ? null
                    : new CandidateResumeDto
                    {
                        Id = profile.Id.ToString(),
                        FileName = Path.GetFileName(profile.ResumeUrl),
                        FileUrl = profile.ResumeUrl,
                        UploadedAt = profile.User.UpdatedAt ?? profile.User.CreatedAt ?? DateTime.UtcNow
                    }
            };
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
    }
}
