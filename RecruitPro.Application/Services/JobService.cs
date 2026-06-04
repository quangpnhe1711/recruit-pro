using AutoMapper;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using System.Text.Json;

namespace RecruitPro.Application.Services
{
    public class JobService : IJobService
    {
        private readonly IJobRepository _jobRepository;
        private readonly ICandidateProfileRepository _candidateProfileRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public JobService(IJobRepository jobRepository, ICandidateProfileRepository candidateProfileRepository, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _jobRepository = jobRepository;
            _candidateProfileRepository = candidateProfileRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ApiResponse<JobsListingResponseDto>> GetJobsAsync(int currentPage = 1, int pageSize = 10)
        {
            var (jobs, total) = await _jobRepository.GetApprovedPagedAsync(currentPage, pageSize);
            var jobCards = _mapper.Map<List<JobCardDto>>(jobs);

            return ApiResponse<JobsListingResponseDto>.Ok(new JobsListingResponseDto
            {
                Jobs = jobCards,
                Total = total,
                Page = currentPage,
                Limit = pageSize
            });
        }

        public async Task<ApiResponse<JobSearchResponseDto>> SearchJobsAsync(JobQueryRequest request)
        {
            var (jobs, total) = await _jobRepository.SearchApprovedAsync(
                request.Keyword,
                request.EmploymentTypes,
                request.Skills,
                request.SortBy,
                request.Page,
                request.PageSize);

            var items = jobs.Select(MapJobListItem).ToList();
            return ApiResponse<JobSearchResponseDto>.Ok(new JobSearchResponseDto
            {
                Items = items,
                Meta = BuildMeta(request.Page, request.PageSize, total)
            });
        }

        public async Task<ApiResponse<JobFiltersResponseDto>> GetFiltersAsync()
        {
            var skills = await _jobRepository.GetAllSkillNamesAsync();
            return ApiResponse<JobFiltersResponseDto>.Ok(new JobFiltersResponseDto
            {
                SalaryRanges =
                [
                    new SalaryRangeDto { Label = "$50k - $80k", Min = 50000, Max = 80000 },
                    new SalaryRangeDto { Label = "$80k - $120k", Min = 80000, Max = 120000 },
                    new SalaryRangeDto { Label = "$120k - $180k", Min = 120000, Max = 180000 },
                    new SalaryRangeDto { Label = "$180k+", Min = 180000, Max = null }
                ],
                EmploymentTypes = ["Full-time", "Contract", "Internship", "Part-time"],
                Skills = skills.ToList()
            });
        }

        public async Task<ApiResponse<JobDetailDto>> GetJobDetailAsync(string jobId)
        {
            var job = await GetJobAsync(jobId);
            return ApiResponse<JobDetailDto>.Ok(MapLegacyJobDetail(job));
        }

        public async Task<ApiResponse<JobDetailScreenDto>> GetJobScreenDetailAsync(string jobId)
        {
            var job = await GetJobAsync(jobId);
            var applications = await _jobRepository.GetApplicationsByJobIdAsync(job.Id);

            return ApiResponse<JobDetailScreenDto>.Ok(new JobDetailScreenDto
            {
                Id = job.Id.ToString(),
                Title = job.Title,
                Location = $"{job.Location} ({job.WorkMode})",
                PostedAt = job.CreatedAt,
                Status = job.Status.ToString(),
                SalaryRange = new SalaryRangeDto
                {
                    Min = job.SalaryMin,
                    Max = job.SalaryMax,
                    Label = "USD"
                },
                Department = job.Department?.Name ?? string.Empty,
                JobType = $"{MapEmploymentType(job.EmploymentType)}, {job.WorkMode}",
                VacancyCount = job.VacancyCount,
                Description = ParseJsonArray(job.Description),
                Requirements = ParseJsonArray(job.Requirements),
                ApplicationSummary = new ApplicationSummaryDto
                {
                    TotalApplications = applications.Count,
                    Funnel =
                    [
                        new FunnelCountDto { Label = "Applications", Count = applications.Count },
                        new FunnelCountDto { Label = "Screening", Count = applications.Count(x => x.Status == ApplicationStatus.Reviewing) },
                        new FunnelCountDto { Label = "Interviews", Count = applications.Count(x => x.Status == ApplicationStatus.Interviewing) },
                        new FunnelCountDto { Label = "Finalist", Count = applications.Count(x => x.Status == ApplicationStatus.ManagerReview || x.Status == ApplicationStatus.Accepted) }
                    ]
                }
            });
        }

        public async Task<ApiResponse<IReadOnlyList<RecentJobApplicationDto>>> GetRecentApplicationsAsync(string jobId)
        {
            var job = await GetJobAsync(jobId);
            var applications = await _jobRepository.GetRecentApplicationsByJobIdAsync(job.Id, 5);

            var items = applications.Select(app => new RecentJobApplicationDto
            {
                Id = app.Id.ToString(),
                CandidateId = app.UserId.ToString(),
                CandidateName = app.User.FullName,
                AvatarUrl = app.User.AvatarUrl,
                AppliedAt = app.AppliedAt,
                Status = app.Status.ToString(),
                Score = null
            }).ToList();

            return ApiResponse<IReadOnlyList<RecentJobApplicationDto>>.Ok(items);
        }

        public async Task<ApiResponse<ApplyJobResponseDto>> ApplyAsync(Guid userId, string jobId, ApplyJobRequest request)
        {
            var job = await GetJobAsync(jobId);
            var profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
            if (profile == null)
            {
                throw new NotFoundException("Candidate profile not found.");
            }

            if (await _jobRepository.CandidateAlreadyAppliedAsync(userId, job.Id))
            {
                return ApiResponse<ApplyJobResponseDto>.BadRequest("Candidate already applied for this job.");
            }

            var application = new RecruitPro.Domain.Entities.Application
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                JobId = job.Id,
                Status = ApplicationStatus.Pending,
                AppliedAt = DateTime.UtcNow
            };

            await _unitOfWork.BeginTransactionAsync();
            await _jobRepository.AddApplicationAsync(application);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            return ApiResponse<ApplyJobResponseDto>.Created(new ApplyJobResponseDto
            {
                ApplicationId = application.Id.ToString(),
                Status = application.Status.ToString()
            }, "Application submitted successfully");
        }

        public async Task<ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>> GetJobApplicationsAsync(string jobId, int page = 1, int pageSize = 10)
        {
            var job = await GetJobAsync(jobId);
            var (applications, total) = await _jobRepository.GetJobApplicationsAsync(job.Id, page, pageSize);
            var applicationDtos = applications.Select(MapApplicationToDto).ToList();

            return ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>.Ok(new PaginatedResponseDto<ApplicationListItemDto>
            {
                Items = applicationDtos,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = total
            });
        }

        public async Task<ApiResponse<HiringFunnelStatisticsDto>> GetJobStatisticsAsync(string jobId)
        {
            var job = await GetJobAsync(jobId);
            var applications = await _jobRepository.GetApplicationsByJobIdAsync(job.Id);

            return ApiResponse<HiringFunnelStatisticsDto>.Ok(new HiringFunnelStatisticsDto
            {
                Applied = applications.Count,
                Screening = applications.Count(a => a.Status == ApplicationStatus.Reviewing),
                Interview = applications.Count(a => a.Status == ApplicationStatus.Interviewing),
                Offer = applications.Count(a => a.Status == ApplicationStatus.ManagerReview),
                Hired = applications.Count(a => a.Status == ApplicationStatus.Accepted)
            });
        }

        public async Task<ApiResponse<JobDetailDto>> UpdateJobStatusAsync(string jobId, UpdateJobStatusRequest request)
        {
            var job = await GetJobAsync(jobId, false);
            if (!Enum.TryParse<JobStatus>(request.Status, true, out var newStatus))
            {
                throw new ArgumentException($"Invalid job status: {request.Status}");
            }

            job.Status = newStatus;
            await _jobRepository.UpdateAsync(job);

            return ApiResponse<JobDetailDto>.Ok(MapLegacyJobDetail(job));
        }

        private async Task<Job> GetJobAsync(string jobId, bool asNoTracking = true)
        {
            if (!Guid.TryParse(jobId, out var jobGuid))
            {
                throw new NotFoundException($"Job with ID {jobId} not found.");
            }

            var job = await _jobRepository.GetByIdAsync(jobGuid);
            if (job == null)
            {
                throw new NotFoundException($"Job with ID {jobId} not found.");
            }

            return job;
        }

        private static JobListItemDto MapJobListItem(Job job)
        {
            return new JobListItemDto
            {
                Id = job.Id.ToString(),
                Title = job.Title,
                ShortPitch = job.ShortPitch,
                Department = job.Department?.Name ?? string.Empty,
                Location = job.Location,
                WorkMode = job.WorkMode.ToString(),
                SalaryMin = job.SalaryMin,
                SalaryMax = job.SalaryMax,
                PostedAt = job.CreatedAt,
                Tags = job.JobSkills.Select(x => x.Skill.Name).Distinct().ToList(),
                ShortDescription = ParseJsonArray(job.Description).FirstOrDefault() ?? string.Empty,
                EmploymentType = MapEmploymentType(job.EmploymentType)
            };
        }

        private static JobDetailDto MapLegacyJobDetail(Job job)
        {
            return new JobDetailDto
            {
                Id = job.Id,
                Title = job.Title,
                Department = job.Department?.Name ?? string.Empty,
                Location = job.Location,
                WorkMode = job.WorkMode.ToString(),
                Requirements = ParseJsonArray(job.Requirements),
                Skills = job.JobSkills.Select(js => new JobSkillDto
                {
                    Id = js.SkillId,
                    Name = js.Skill.Name,
                    MinYearsExperience = js.MinYearsExperience,
                    IsRequired = js.IsRequired
                }).ToList(),
                SalaryMin = job.SalaryMin,
                SalaryMax = job.SalaryMax,
                Deadline = job.Deadline,
                JobType = $"{MapEmploymentType(job.EmploymentType)} / {job.WorkMode}",
                SalaryRange = $"{job.SalaryMin:0}-{job.SalaryMax:0}",
                Posted = job.CreatedAt?.ToString("yyyy-MM-dd") ?? string.Empty,
                VacancyCount = job.VacancyCount,
                Status = job.Status.ToString(),
                Description = ParseJsonArray(job.Description)
            };
        }

        private static ApplicationListItemDto MapApplicationToDto(RecruitPro.Domain.Entities.Application application)
        {
            return new ApplicationListItemDto
            {
                Id = application.Id.ToString(),
                Candidate = new ApplicationCandidateSummaryDto
                {
                    Id = application.UserId.ToString(),
                    FullName = application.User.FullName,
                    Email = application.User.Email,
                    AvatarUrl = application.User.AvatarUrl,
                    CurrentPosition = application.User.CandidateProfile?.CurrentPosition
                },
                Job = new ApplicationJobSummaryDto
                {
                    Id = application.Job.Id.ToString(),
                    Title = application.Job.Title,
                    Department = new DepartmentDto
                    {
                        Id = application.Job.Department?.Id.ToString() ?? string.Empty,
                        Name = application.Job.Department?.Name ?? string.Empty,
                        Description = application.Job.Department?.Description
                    }
                },
                Status = application.Status.ToString(),
                AppliedAt = application.AppliedAt ?? DateTime.UtcNow,
                ReviewedBy = application.ReviewedByNavigation == null ? null : new UserDto
                {
                    Id = application.ReviewedByNavigation.Id,
                    FullName = application.ReviewedByNavigation.FullName,
                    Email = application.ReviewedByNavigation.Email,
                    AvatarUrl = application.ReviewedByNavigation.AvatarUrl,
                    Phone = application.ReviewedByNavigation.Phone,
                    Roles = application.ReviewedByNavigation.UserRoles.Select(x => x.Role.Name).ToList()
                },
                NextStep = application.Interviews.Any() ? "Interview scheduled" : "In review"
            };
        }

        private static List<string> ParseJsonArray(string? jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                return [];
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return doc.RootElement.EnumerateArray()
                        .Select(el => el.GetString() ?? string.Empty)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();
                }
            }
            catch
            {
            }

            return [jsonString];
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

        private static string MapEmploymentType(EmploymentType type)
        {
            return type switch
            {
                EmploymentType.FullTime => "Full-time",
                EmploymentType.PartTime => "Part-time",
                EmploymentType.Internship => "Internship",
                EmploymentType.Contract => "Contract",
                _ => type.ToString()
            };
        }
    }
}
