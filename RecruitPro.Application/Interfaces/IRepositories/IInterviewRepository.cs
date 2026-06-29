using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface IInterviewRepository
{
    Task<Interview?> GetNextAsync(DateTime fromDate);
    Task<int> CountOnDateAsync(DateTime date);
    Task<int> CountUpcomingScheduledAsync(DateTime fromDate);
    Task<IReadOnlyList<(DateTime Month, int Count)>> GetMonthlyCompletedVolumeAsync(DateTime startMonth, int monthCount);
    Task<(IReadOnlyList<Interview> Interviews, int Total)> GetPagedAsync(int page, int pageSize, string? keyword, InterviewStatus? status, DateTime? startDate, DateTime? endDate, Guid scopeUserId);
    Task<Interview?> GetTrackedByIdAsync(Guid interviewId);
    Task AddAsync(Interview interview);
    Task DeleteAsync(Interview interview);
}
