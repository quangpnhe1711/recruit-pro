using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories;

public class InterviewRepository : IInterviewRepository
{
    private readonly AppDbContext _context;

    public InterviewRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Interview?> GetNextAsync(DateTime fromDate)
    {
        return _context.Interviews
            .AsNoTracking()
            .OrderBy(interview => interview.InterviewDate)
            .FirstOrDefaultAsync(interview => interview.InterviewDate >= fromDate);
    }

    public Task<int> CountOnDateAsync(DateTime date)
    {
        return _context.Interviews
            .AsNoTracking()
            .CountAsync(interview => interview.InterviewDate.Date == date.Date);
    }

    public async Task<(IReadOnlyList<Interview> Interviews, int Total)> GetPagedAsync(int page, int pageSize, string? keyword, InterviewStatus? status, DateTime? startDate, DateTime? endDate)
    {
        IQueryable<Interview> query = BuildInterviewQuery();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string loweredKeyword = keyword.Trim().ToLowerInvariant();
            query = query.Where(interview => interview.Application.User.FullName.ToLower().Contains(loweredKeyword) || interview.Application.Job.Title.ToLower().Contains(loweredKeyword));
        }

        if (status.HasValue)
        {
            query = query.Where(interview => interview.Status == status.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(interview => interview.InterviewDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(interview => interview.InterviewDate <= endDate.Value);
        }

        int total = await query.CountAsync();
        List<Interview> interviews = await query
            .OrderBy(interview => interview.InterviewDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (interviews, total);
    }

    public Task<Interview?> GetTrackedByIdAsync(Guid interviewId)
    {
        return _context.Interviews.FirstOrDefaultAsync(interview => interview.Id == interviewId);
    }

    public async Task AddAsync(Interview interview)
    {
        await _context.Interviews.AddAsync(interview);
    }

    public Task DeleteAsync(Interview interview)
    {
        _context.Interviews.Remove(interview);
        return Task.CompletedTask;
    }

    private IQueryable<Interview> BuildInterviewQuery()
    {
        return _context.Interviews
            .AsNoTracking()
            .Include(interview => interview.Application)
                .ThenInclude(application => application.User)
                    .ThenInclude(user => user.CandidateProfile)
            .Include(interview => interview.Application)
                .ThenInclude(application => application.Job)
                    .ThenInclude(job => job.Department);
    }
}
