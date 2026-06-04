using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;
using JobApplication = RecruitPro.Domain.Entities.Application;

namespace RecruitPro.Infrastructure.Repositories
{
    public class JobRepository : IJobRepository
    {
        private readonly AppDbContext _context;

        public JobRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(IReadOnlyList<Job> Jobs, int Total)> GetApprovedPagedAsync(int currentPage, int pageSize)
        {
            var query = BuildJobQuery()
     .Where(job => !Constants.NOT_SHOW_JOB_STATUS.Contains(job.Status));
            var total = await query.CountAsync();
            var jobs = await query
                .OrderByDescending(job => job.CreatedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (jobs, total);
        }

        public async Task<(IReadOnlyList<Job> Jobs, int Total)> SearchApprovedAsync(string? keyword, IReadOnlyCollection<string> employmentTypes, IReadOnlyCollection<string> skills, string? sortBy, int currentPage, int pageSize)
        {
            var query = BuildJobQuery()
                .Where(job => !Constants.NOT_SHOW_JOB_STATUS.Contains(job.Status));
                
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var loweredKeyword = keyword.Trim().ToLowerInvariant();
                query = query.Where(job =>
                    job.Title.ToLower().Contains(loweredKeyword) ||
                    (job.Description != null && job.Description.ToLower().Contains(loweredKeyword)) ||
                    (job.Department != null && job.Department.Name.ToLower().Contains(loweredKeyword)));
            }

            if (employmentTypes.Count > 0)
            {
                var mappedTypes = employmentTypes
                    .Select(ParseEmploymentType)
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .ToList();

                if (mappedTypes.Count > 0)
                {
                    query = query.Where(job => mappedTypes.Contains(job.EmploymentType));
                }
            }

            if (skills.Count > 0)
            {
                var loweredSkills = skills.Select(x => x.Trim().ToLowerInvariant()).ToList();
                query = query.Where(job => job.JobSkills.Any(js => loweredSkills.Contains(js.Skill.Name.ToLower())));
            }

            query = sortBy?.Trim().ToLowerInvariant() switch
            {
                "salarydesc" => query.OrderByDescending(job => job.SalaryMax).ThenByDescending(job => job.CreatedAt),
                _ => query.OrderByDescending(job => job.CreatedAt)
            };
            var total = await query.CountAsync();
            var jobs = await query
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (jobs, total);
        }

        public async Task<(IReadOnlyList<Job> Jobs, int Total)> GetPagedAsync(string? department, string? approvalStatus, int currentPage, int pageSize)
        {
            var query = BuildJobQuery();

            if (!string.IsNullOrWhiteSpace(department))
            {
                var loweredDepartment = department.Trim().ToLowerInvariant();
                query = query.Where(job => job.Department != null && job.Department.Name.ToLower().Contains(loweredDepartment));
            }

            var parsedStatus = ParseJobStatus(approvalStatus);
            if (parsedStatus.HasValue)
            {
                query = query.Where(job => job.Status == parsedStatus.Value);
            }

            var total = await query.CountAsync();
            var jobs = await query
                .OrderByDescending(job => job.CreatedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (jobs, total);
        }

        public async Task<Job?> GetByIdAsync(Guid id)
        {
            return await BuildJobQuery().FirstOrDefaultAsync(job => job.Id == id);
        }

        public async Task<(IReadOnlyList<JobApplication> Applications, int Total)> GetJobApplicationsAsync(Guid jobId, int currentPage, int pageSize)
        {
            var query = BuildApplicationQuery().Where(app => app.JobId == jobId);
            var total = await query.CountAsync();
            var applications = await query
                .OrderByDescending(app => app.AppliedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (applications, total);
        }

        public async Task<IReadOnlyList<JobApplication>> GetApplicationsByJobIdAsync(Guid jobId)
        {
            return await BuildApplicationQuery()
                .Where(app => app.JobId == jobId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<JobApplication>> GetApplicationsByUserIdAsync(Guid userId)
        {
            return await BuildApplicationQuery()
                .Where(app => app.UserId == userId)
                .OrderByDescending(app => app.AppliedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<JobApplication>> GetRecentApplicationsByJobIdAsync(Guid jobId, int take)
        {
            return await BuildApplicationQuery()
                .Where(app => app.JobId == jobId)
                .OrderByDescending(app => app.AppliedAt)
                .Take(take)
                .ToListAsync();
        }

        public Task<bool> CandidateAlreadyAppliedAsync(Guid userId, Guid jobId)
        {
            return _context.Applications.AnyAsync(app => app.UserId == userId && app.JobId == jobId);
        }

        public async Task AddApplicationAsync(JobApplication application)
        {
            await _context.Applications.AddAsync(application);
        }

        public Task UpdateApplicationAsync(JobApplication application)
        {
            _context.Applications.Update(application);
            return Task.CompletedTask;
        }

        public async Task AddAsync(Job job)
        {
            await _context.Jobs.AddAsync(job);
        }

        public Task DeleteAsync(Job job)
        {
            _context.Jobs.Remove(job);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<string>> GetAllSkillNamesAsync()
        {
            return await _context.Skills
                .AsNoTracking()
                .OrderBy(skill => skill.Name)
                .Select(skill => skill.Name)
                .ToListAsync();
        }

        public async Task UpdateAsync(Job job)
        {
            _context.Jobs.Update(job);
            await _context.SaveChangesAsync();
        }

        private IQueryable<Job> BuildJobQuery()
        {
            return _context.Jobs
                .AsNoTracking()
                .Include(job => job.Department)
                .Include(job => job.CreatedByNavigation)
                .Include(job => job.ApprovedByNavigation)
                .Include(job => job.JobSkills)
                    .ThenInclude(jobSkill => jobSkill.Skill)
                .Include(job => job.Applications);
        }

        private IQueryable<JobApplication> BuildApplicationQuery()
        {
            return _context.Applications
                .AsNoTracking()
                .Include(app => app.User)
                    .ThenInclude(user => user.CandidateProfile)
                .Include(app => app.Job)
                    .ThenInclude(job => job.Department)
                .Include(app => app.Interviews)
                .Include(app => app.ReviewedByNavigation);
        }

        private static EmploymentType? ParseEmploymentType(string value)
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "full-time" or "fulltime" => EmploymentType.FullTime,
                "part-time" or "parttime" => EmploymentType.PartTime,
                "internship" => EmploymentType.Internship,
                "contract" => EmploymentType.Contract,
                _ => null
            };
        }

        private static JobStatus? ParseJobStatus(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "draft" => JobStatus.Draft,
                "pending" or "pendingapproval" => JobStatus.PendingApproval,
                "approved" => JobStatus.Approved,
                "closed" => JobStatus.Closed,
                "rejected" => JobStatus.Rejected,
                _ => null
            };
        }
    }
}
