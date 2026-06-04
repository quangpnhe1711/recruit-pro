using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;
using JobApplication = RecruitPro.Domain.Entities.Application;

namespace RecruitPro.Infrastructure.Repositories;

public class HrRepository : IHrRepository
{
    private readonly AppDbContext _context;

    public HrRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Interview?> GetNextInterviewAsync(DateTime fromDate)
    {
        return _context.Interviews
            .AsNoTracking()
            .OrderBy(x => x.InterviewDate)
            .FirstOrDefaultAsync(x => x.InterviewDate >= fromDate);
    }

    public async Task<IReadOnlyList<JobApplication>> GetRecentApplicationsAsync(int take)
    {
        return await _context.Applications
            .AsNoTracking()
            .Include(x => x.User)
                .ThenInclude(x => x.CandidateProfile)
            .Include(x => x.Job)
            .OrderByDescending(x => x.AppliedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Job>> GetPendingApprovalJobsAsync(int take)
    {
        return await _context.Jobs
            .AsNoTracking()
            .Include(x => x.Department)
            .Where(x => x.Status == JobStatus.PendingApproval)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public Task<int> CountApprovedJobsAsync()
    {
        return _context.Jobs.CountAsync(x => x.Status == JobStatus.Approved);
    }

    public Task<int> CountApplicationsAsync()
    {
        return _context.Applications.CountAsync();
    }

    public Task<int> CountInterviewsOnDateAsync(DateTime date)
    {
        return _context.Interviews.CountAsync(x => x.InterviewDate.Date == date.Date);
    }

    public async Task<(IReadOnlyList<Job> Jobs, int Total)> GetJobsAsync(string? department, JobStatus? status, int page, int pageSize)
    {
        var query = _context.Jobs
            .AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.Applications)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.Trim().ToLowerInvariant();
            query = query.Where(x => x.Department != null && x.Department.Name.ToLower().Contains(loweredDepartment));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var total = await query.CountAsync();
        var jobs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (jobs, total);
    }

    public Task<Department?> GetDepartmentByNameAsync(string departmentName)
    {
        return _context.Departments.FirstOrDefaultAsync(x => x.Name == departmentName);
    }

    public Task<Job?> GetJobByIdAsync(Guid jobId)
    {
        return _context.Jobs
            .Include(x => x.Department)
            .Include(x => x.Applications)
            .FirstOrDefaultAsync(x => x.Id == jobId);
    }

    public Task AddJobAsync(Job job)
    {
        return _context.Jobs.AddAsync(job).AsTask();
    }

    public Task DeleteJobAsync(Job job)
    {
        _context.Jobs.Remove(job);
        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<CandidateProfile> Candidates, int Total)> GetCandidatesAsync(int page, int pageSize, string? keyword)
    {
        var query = _context.CandidateProfiles
            .AsNoTracking()
            .Include(x => x.User)
                .ThenInclude(x => x.Applications)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var loweredKeyword = keyword.Trim().ToLowerInvariant();
            query = query.Where(x => x.User.FullName.ToLower().Contains(loweredKeyword) || x.User.Email.ToLower().Contains(loweredKeyword));
        }

        var total = await query.CountAsync();
        var candidates = await query
            .OrderBy(x => x.User.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (candidates, total);
    }

    public async Task<(IReadOnlyList<JobApplication> Applications, int Total)> GetApplicationsAsync(int page, int pageSize, string? keyword, string? department, ApplicationStatus? status)
    {
        var query = _context.Applications
            .AsNoTracking()
            .Include(x => x.User)
                .ThenInclude(x => x.CandidateProfile)
            .Include(x => x.Job)
                .ThenInclude(x => x.Department)
            .Include(x => x.ReviewedByNavigation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var loweredKeyword = keyword.Trim().ToLowerInvariant();
            query = query.Where(x => x.User.FullName.ToLower().Contains(loweredKeyword) || x.Job.Title.ToLower().Contains(loweredKeyword));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.Trim().ToLowerInvariant();
            query = query.Where(x => x.Job.Department != null && x.Job.Department.Name.ToLower().Contains(loweredDepartment));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var total = await query.CountAsync();
        var applications = await query
            .OrderByDescending(x => x.AppliedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (applications, total);
    }

    public Task<JobApplication?> GetApplicationByIdAsync(Guid applicationId)
    {
        return _context.Applications
            .AsNoTracking()
            .Include(x => x.User)
                .ThenInclude(x => x.CandidateProfile)
            .Include(x => x.Job)
                .ThenInclude(x => x.Department)
            .FirstOrDefaultAsync(x => x.Id == applicationId);
    }

    public async Task<(IReadOnlyList<Interview> Interviews, int Total)> GetInterviewsAsync(int page, int pageSize, string? keyword, InterviewStatus? status, DateTime? startDate, DateTime? endDate)
    {
        var query = _context.Interviews
            .Include(x => x.Application)
                .ThenInclude(x => x.User)
                    .ThenInclude(x => x.CandidateProfile)
            .Include(x => x.Application)
                .ThenInclude(x => x.Job)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var loweredKeyword = keyword.Trim().ToLowerInvariant();
            query = query.Where(x => x.Application.User.FullName.ToLower().Contains(loweredKeyword) || x.Application.Job.Title.ToLower().Contains(loweredKeyword));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(x => x.InterviewDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(x => x.InterviewDate <= endDate.Value);
        }

        var total = await query.CountAsync();
        var interviews = await query
            .OrderBy(x => x.InterviewDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (interviews, total);
    }

    public Task<Interview?> GetInterviewByIdAsync(Guid interviewId)
    {
        return _context.Interviews.FirstOrDefaultAsync(x => x.Id == interviewId);
    }

    public Task<CandidateProfile?> GetFirstCandidateAsync()
    {
        return _context.CandidateProfiles
            .AsNoTracking()
            .Include(x => x.User)
                .ThenInclude(x => x.Applications)
                    .ThenInclude(x => x.Job)
            .OrderBy(x => x.User.FullName)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<User>> GetInterviewersAsync(int take)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .Where(x => x.UserRoles.Any(r => r.Role.Name == "HR" || r.Role.Name == "Manager"))
            .OrderBy(x => x.FullName)
            .Take(take)
            .ToListAsync();
    }

    public Task AddInterviewAsync(Interview interview)
    {
        return _context.Interviews.AddAsync(interview).AsTask();
    }

    public Task DeleteInterviewAsync(Interview interview)
    {
        _context.Interviews.Remove(interview);
        return Task.CompletedTask;
    }
}
