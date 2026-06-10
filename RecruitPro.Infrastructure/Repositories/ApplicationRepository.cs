using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;
using JobApplication = RecruitPro.Domain.Entities.Application;

namespace RecruitPro.Infrastructure.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly AppDbContext _context;

    public ApplicationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<int> CountAsync()
    {
        return _context.Applications.AsNoTracking().CountAsync();
    }

    public async Task<IReadOnlyList<JobApplication>> GetRecentAsync(int take)
    {
        return await BuildApplicationQuery()
            .OrderByDescending(application => application.AppliedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<JobApplication> Applications, int Total)> GetPagedAsync(int page, int pageSize, string? keyword, string? department, ApplicationStatus? status)
    {
        IQueryable<JobApplication> query = BuildApplicationQuery();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string loweredKeyword = keyword.Trim().ToLowerInvariant();
            query = query.Where(application => application.User.FullName.ToLower().Contains(loweredKeyword) || application.Job.Title.ToLower().Contains(loweredKeyword));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            string loweredDepartment = department.Trim().ToLowerInvariant();
            query = query.Where(application => application.Job.Department != null && application.Job.Department.Name.ToLower().Contains(loweredDepartment));
        }

        if (status.HasValue)
        {
            query = query.Where(application => application.Status == status.Value);
        }

        int total = await query.CountAsync();
        List<JobApplication> applications = await query
            .OrderByDescending(application => application.AppliedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (applications, total);
    }

    public async Task<(IReadOnlyList<JobApplication> Applications, int Total)> GetByJobIdAsync(Guid jobId, int currentPage, int pageSize)
    {
        IQueryable<JobApplication> query = BuildApplicationQuery().Where(application => application.JobId == jobId);
        int total = await query.CountAsync();
        List<JobApplication> applications = await query
            .OrderByDescending(application => application.AppliedAt)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (applications, total);
    }

    public async Task<IReadOnlyList<JobApplication>> GetAllByJobIdAsync(Guid jobId)
    {
        return await BuildApplicationQuery()
            .Where(application => application.JobId == jobId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<JobApplication>> GetRecentByJobIdAsync(Guid jobId, int take)
    {
        return await BuildApplicationQuery()
            .Where(application => application.JobId == jobId)
            .OrderByDescending(application => application.AppliedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<JobApplication>> GetByUserIdAsync(Guid userId)
    {
        return await BuildApplicationQuery()
            .Where(application => application.UserId == userId)
            .OrderByDescending(application => application.AppliedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<JobApplication>> GetManagerReviewQueueAsync(string? keyword)
    {
        IQueryable<JobApplication> query = BuildApplicationQuery()
            .Where(application =>
                application.Status != ApplicationStatus.Accepted &&
                application.Status != ApplicationStatus.Rejected &&
                application.Interviews.Any(interview => interview.Status == InterviewStatus.Completed));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string loweredKeyword = keyword.Trim().ToLowerInvariant();
            query = query.Where(application =>
                application.User.FullName.ToLower().Contains(loweredKeyword) ||
                application.Job.Title.ToLower().Contains(loweredKeyword));
        }

        return await query
            .OrderByDescending(application => application.Interviews
                .Where(interview => interview.Status == InterviewStatus.Completed)
                .Max(interview => (DateTime?)interview.InterviewDate) ?? application.AppliedAt)
            .ThenByDescending(application => application.AppliedAt)
            .ToListAsync();
    }

    public async Task<Dictionary<ApplicationStatus, int>> GetStatusCountsAsync()
    {
        return await _context.Applications
            .AsNoTracking()
            .GroupBy(application => application.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.Status, item => item.Count);
    }

    public async Task<IReadOnlyList<(string DepartmentName, int AverageDays)>> GetAverageReviewCycleByDepartmentAsync()
    {
        var projectedItems = await _context.Applications
            .AsNoTracking()
            .Where(application =>
                application.AppliedAt.HasValue &&
                application.Interviews.Any(interview => interview.Status == InterviewStatus.Completed))
            .Select(application => new
            {
                DepartmentName = application.Job.Department != null ? application.Job.Department.Name : "General",
                AppliedAt = application.AppliedAt!.Value,
                LatestCompletedInterview = application.Interviews
                    .Where(interview => interview.Status == InterviewStatus.Completed)
                    .Max(interview => (DateTime?)interview.InterviewDate)
            })
            .ToListAsync();

        return projectedItems
            .Where(item => item.LatestCompletedInterview.HasValue)
            .GroupBy(item => item.DepartmentName)
            .Select(group => (
                DepartmentName: group.Key,
                AverageDays: (int)Math.Round(group.Average(item =>
                    Math.Max((item.LatestCompletedInterview!.Value - item.AppliedAt).TotalDays, 0)), MidpointRounding.AwayFromZero)))
            .OrderBy(metric => metric.AverageDays)
            .ToList();
    }

    public Task<int> CountActiveCandidatesAsync()
    {
        return _context.Applications
            .AsNoTracking()
            .Where(application => application.Status != ApplicationStatus.Accepted && application.Status != ApplicationStatus.Rejected)
            .Select(application => application.UserId)
            .Distinct()
            .CountAsync();
    }

    public async Task<double?> GetAverageReviewCycleDaysAsync()
    {
        var projectedItems = await _context.Applications
            .AsNoTracking()
            .Where(application =>
                application.AppliedAt.HasValue &&
                application.Interviews.Any(interview => interview.Status == InterviewStatus.Completed))
            .Select(application => new
            {
                AppliedAt = application.AppliedAt!.Value,
                LatestCompletedInterview = application.Interviews
                    .Where(interview => interview.Status == InterviewStatus.Completed)
                    .Max(interview => (DateTime?)interview.InterviewDate)
            })
            .ToListAsync();

        var cycleDays = projectedItems
            .Where(item => item.LatestCompletedInterview.HasValue)
            .Select(item => Math.Max((item.LatestCompletedInterview!.Value - item.AppliedAt).TotalDays, 0))
            .ToList();

        return cycleDays.Count == 0 ? null : cycleDays.Average();
    }

    public async Task<IReadOnlyList<(DateTime Month, int Count)>> GetMonthlyApplicationVolumeAsync(DateTime startMonth, int monthCount)
    {
        DateTime endMonth = startMonth.AddMonths(monthCount);

        var groupedItems = await _context.Applications
            .AsNoTracking()
            .Where(application =>
                application.AppliedAt.HasValue &&
                application.AppliedAt.Value >= startMonth &&
                application.AppliedAt.Value < endMonth)
            .GroupBy(application => new
            {
                application.AppliedAt!.Value.Year,
                application.AppliedAt!.Value.Month
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Count = group.Count()
            })
            .ToListAsync();

        return groupedItems
            .Select(item => (new DateTime(item.Year, item.Month, 1), item.Count))
            .OrderBy(item => item.Item1)
            .ToList();
    }

    public async Task<IReadOnlyList<(string DepartmentName, int ActiveApplications, int OfferedCandidates, int AcceptedCandidates)>> GetDepartmentPipelineSnapshotAsync()
    {
        var groupedItems = await _context.Applications
            .AsNoTracking()
            .GroupBy(application => application.Job.Department != null ? application.Job.Department.Name : "General")
            .Select(group => new
            {
                DepartmentName = group.Key,
                ActiveApplications = group.Count(application =>
                    application.Status != ApplicationStatus.Accepted &&
                    application.Status != ApplicationStatus.Rejected),
                OfferedCandidates = group.Count(application => application.Status == ApplicationStatus.ManagerReview),
                AcceptedCandidates = group.Count(application => application.Status == ApplicationStatus.Accepted)
            })
            .ToListAsync();

        return groupedItems
            .Select(item => (item.DepartmentName, item.ActiveApplications, item.OfferedCandidates, item.AcceptedCandidates))
            .OrderByDescending(item => item.ActiveApplications)
            .ToList();
    }

    public Task<JobApplication?> GetByIdAsync(Guid applicationId)
    {
        return BuildApplicationQuery().FirstOrDefaultAsync(application => application.Id == applicationId);
    }

    public Task<JobApplication?> GetTrackedByIdAsync(Guid applicationId)
    {
        return BuildTrackedApplicationQuery().FirstOrDefaultAsync(application => application.Id == applicationId);
    }

    public Task<bool> CandidateAlreadyAppliedAsync(Guid userId, Guid jobId)
    {
        return _context.Applications.AsNoTracking().AnyAsync(application => application.UserId == userId && application.JobId == jobId);
    }

    public async Task AddAsync(JobApplication application)
    {
        await _context.Applications.AddAsync(application);
    }

    public Task UpdateAsync(JobApplication application)
    {
        _context.Applications.Update(application);
        return Task.CompletedTask;
    }

    private IQueryable<JobApplication> BuildApplicationQuery()
    {
        return _context.Applications
            .AsNoTracking()
            .Include(application => application.User)
                .ThenInclude(user => user.CandidateProfile)
                    .ThenInclude(profile => profile.Skills)
            .Include(application => application.Job)
                .ThenInclude(job => job.Department)
            .Include(application => application.Job)
                .ThenInclude(job => job.JobSkills)
                    .ThenInclude(jobSkill => jobSkill.Skill)
            .Include(application => application.Interviews)
            .Include(application => application.ReviewedByNavigation)
                .ThenInclude(user => user.UserRoles)
                    .ThenInclude(userRole => userRole.Role);
    }

    private IQueryable<JobApplication> BuildTrackedApplicationQuery()
    {
        return _context.Applications
            .Include(application => application.User)
                .ThenInclude(user => user.CandidateProfile)
                    .ThenInclude(profile => profile.Skills)
            .Include(application => application.Job)
                .ThenInclude(job => job.Department)
            .Include(application => application.Interviews)
            .Include(application => application.ReviewedByNavigation);
    }
}
