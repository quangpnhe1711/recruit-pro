using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;

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
            IQueryable<Job> query = BuildJobQuery()
                .Where(job => !Constants.NOT_SHOW_JOB_STATUS.Contains(job.Status));

            int total = await query.CountAsync();
            List<Job> jobs = await query
                .OrderByDescending(job => job.CreatedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (jobs, total);
        }

        public async Task<(IReadOnlyList<Job> Jobs, int Total)> SearchApprovedAsync(string? keyword, IReadOnlyCollection<string> employmentTypes, IReadOnlyCollection<string> skills, string? sortBy, int currentPage, int pageSize)
        {
            IQueryable<Job> query = BuildJobQuery()
                .Where(job => !Constants.NOT_SHOW_JOB_STATUS.Contains(job.Status));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string loweredKeyword = keyword.Trim().ToLowerInvariant();
                query = query.Where(job =>
                    job.Title.ToLower().Contains(loweredKeyword) ||
                    (job.Description != null && job.Description.ToLower().Contains(loweredKeyword)) ||
                    (job.Department != null && job.Department.Name.ToLower().Contains(loweredKeyword)));
            }

            if (employmentTypes.Count > 0)
            {
                List<EmploymentType> mappedTypes = employmentTypes
                    .Select(ParseEmploymentType)
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value)
                    .ToList();

                if (mappedTypes.Count > 0)
                {
                    query = query.Where(job => mappedTypes.Contains(job.EmploymentType));
                }
            }

            if (skills.Count > 0)
            {
                List<string> loweredSkills = skills.Select(value => value.Trim().ToLowerInvariant()).ToList();
                query = query.Where(job => job.JobSkills.Any(jobSkill => loweredSkills.Contains(jobSkill.Skill.Name.ToLower())));
            }

            query = sortBy?.Trim().ToLowerInvariant() switch
            {
                "salarydesc" => query.OrderByDescending(job => job.SalaryMax).ThenByDescending(job => job.CreatedAt),
                _ => query.OrderByDescending(job => job.CreatedAt)
            };

            int total = await query.CountAsync();
            List<Job> jobs = await query
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (jobs, total);
        }

        public async Task<(IReadOnlyList<Job> Jobs, int Total)> GetPagedAsync(string? department, string? approvalStatus, int currentPage, int pageSize, Guid? createdByUserId = null, Guid? ownerScopeUserId = null)
        {
            IQueryable<Job> query = BuildJobQuery();

            if (createdByUserId.HasValue)
            {
                query = query.Where(job => job.CreatedBy == createdByUserId.Value);
            }

            if (ownerScopeUserId.HasValue)
            {
                // Ownership scope (Phase 2.2): an HR/Manager may only see jobs they own — created, assigned
                // as recruiter, or head the department of. SystemAdmin passes null and skips this. Applied
                // DB-side so pagination totals stay correct.
                Guid scope = ownerScopeUserId.Value;
                query = query.Where(job =>
                    job.CreatedBy == scope
                    || job.RecruiterId == scope
                    || (job.Department != null && job.Department.HeadUserId == scope));
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                string loweredDepartment = department.Trim().ToLowerInvariant();
                query = query.Where(job => job.Department != null && job.Department.Name.ToLower().Contains(loweredDepartment));
            }

            JobStatus? parsedStatus = ParseJobStatus(approvalStatus);
            if (parsedStatus.HasValue)
            {
                query = query.Where(job => job.Status == parsedStatus.Value);
            }

            int total = await query.CountAsync();
            List<Job> jobs = await query
                .OrderByDescending(job => job.CreatedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (jobs, total);
        }

        public async Task<(IReadOnlyList<Job> Jobs, int Total)> GetPendingApprovalPagedAsync(string? keyword, string? department, int currentPage, int pageSize, Guid? departmentHeadUserId = null)
        {
            IQueryable<Job> query = BuildJobQuery()
                .Where(job => job.Status == JobStatus.PendingApproval);

            if (departmentHeadUserId.HasValue)
            {
                // Scope to the jobs this DepartmentHead owns (BR-OWN-003). Applied DB-side so pagination
                // stays correct.
                query = query.Where(job => job.Department != null && job.Department.HeadUserId == departmentHeadUserId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string loweredKeyword = keyword.Trim().ToLowerInvariant();
                query = query.Where(job =>
                    job.Title.ToLower().Contains(loweredKeyword) ||
                    job.Location.ToLower().Contains(loweredKeyword) ||
                    (job.Department != null && job.Department.Name.ToLower().Contains(loweredKeyword)) ||
                    job.JobSkills.Any(jobSkill => jobSkill.Skill.Name.ToLower().Contains(loweredKeyword)) ||
                    job.CreatedByNavigation.FullName.ToLower().Contains(loweredKeyword));
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                string loweredDepartment = department.Trim().ToLowerInvariant();
                query = query.Where(job => job.Department != null && job.Department.Name.ToLower().Contains(loweredDepartment));
            }

            int total = await query.CountAsync();
            List<Job> jobs = await query
                .OrderByDescending(job => job.CreatedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (jobs, total);
        }

        public Task<int> CountApprovedJobsAsync()
        {
            return _context.Jobs.AsNoTracking().CountAsync(job => job.Status == JobStatus.Approved);
        }

        public Task<int> CountPendingApprovalJobsAsync()
        {
            return _context.Jobs.AsNoTracking().CountAsync(job => job.Status == JobStatus.PendingApproval);
        }

        public async Task<IReadOnlyList<Job>> GetPendingApprovalJobsAsync(int take, Guid? departmentHeadUserId = null)
        {
            IQueryable<Job> query = _context.Jobs
                .AsNoTracking()
                .Include(job => job.Department)
                .Where(job => job.Status == JobStatus.PendingApproval);

            if (departmentHeadUserId.HasValue)
            {
                query = query.Where(job => job.Department != null && job.Department.HeadUserId == departmentHeadUserId.Value);
            }

            return await query
                .OrderByDescending(job => job.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public Task<Job?> GetByIdAsync(Guid id)
        {
            return BuildJobQuery().FirstOrDefaultAsync(job => job.Id == id);
        }

        public async Task<IReadOnlyList<(string DepartmentName, int OpenRoles, string RecruiterName)>> GetDepartmentOpenRoleSnapshotAsync()
        {
            var projectedItems = await _context.Jobs
                .AsNoTracking()
                .Where(job => job.Status == JobStatus.Approved || job.Status == JobStatus.PendingApproval)
                .Select(job => new
                {
                    DepartmentName = job.Department != null ? job.Department.Name : "General",
                    RecruiterName = job.CreatedByNavigation.FullName,
                    job.CreatedAt
                })
                .ToListAsync();

            return projectedItems
                .GroupBy(item => item.DepartmentName)
                .Select(group =>
                {
                    string recruiterName = group
                        .OrderByDescending(item => item.CreatedAt)
                        .Select(item => item.RecruiterName)
                        .FirstOrDefault() ?? "Unassigned";

                    return new
                    {
                        DepartmentName = group.Key,
                        OpenRoles = group.Count(),
                        RecruiterName = recruiterName
                    };
                })
                .OrderByDescending(item => item.OpenRoles)
                .ThenBy(item => item.DepartmentName)
                .Select(item => (item.DepartmentName, item.OpenRoles, item.RecruiterName))
                .ToList();
        }

        public Task<Job?> GetTrackedByIdAsync(Guid id)
        {
            return BuildTrackedJobQuery().FirstOrDefaultAsync(job => job.Id == id);
        }

        public Task<Department?> GetDepartmentByIdAsync(Guid departmentId)
        {
            return _context.Departments
                .AsNoTracking()
                .Include(department => department.HeadUser)
                .FirstOrDefaultAsync(department => department.Id == departmentId);
        }

        public Task<Department?> GetTrackedDepartmentByIdAsync(Guid departmentId)
        {
            return _context.Departments
                .Include(department => department.HeadUser)
                .FirstOrDefaultAsync(department => department.Id == departmentId);
        }

        public Task<Department?> GetDepartmentByNameAsync(string departmentName)
        {
            return _context.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(department => department.Name == departmentName);
        }

        public async Task<IReadOnlyList<Department>> GetDepartmentsAsync()
        {
            return await _context.Departments
                .AsNoTracking()
                .Include(department => department.HeadUser)
                .OrderBy(department => department.Name)
                .ToListAsync();
        }

        public Task UpdateDepartmentAsync(Department department)
        {
            _context.Departments.Update(department);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<Skill>> GetSkillsAsync()
        {
            return await _context.Skills
                .AsNoTracking()
                .OrderBy(skill => skill.Name)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Job>> GetAllApprovedForSemanticSearchAsync()
        {
            return await BuildJobQuery()
                .Where(job => job.Status == JobStatus.Approved)
                .ToListAsync();
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

        public Task UpdateAsync(Job job)
        {
            _context.Jobs.Update(job);
            return Task.CompletedTask;
        }

        private IQueryable<Job> BuildJobQuery()
        {
            return _context.Jobs
                .AsNoTracking()
                .Include(job => job.Department)
                    .ThenInclude(department => department!.HeadUser)
                .Include(job => job.CreatedByNavigation)
                .Include(job => job.ApprovedByNavigation)
                .Include(job => job.Recruiter)
                .Include(job => job.JobSkills)
                    .ThenInclude(jobSkill => jobSkill.Skill)
                .Include(job => job.Applications);
        }

        private IQueryable<Job> BuildTrackedJobQuery()
        {
            return _context.Jobs
                .Include(job => job.Department)
                .Include(job => job.JobSkills)
                    .ThenInclude(jobSkill => jobSkill.Skill);
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
