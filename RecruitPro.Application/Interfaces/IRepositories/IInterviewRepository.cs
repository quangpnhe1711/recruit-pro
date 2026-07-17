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

    // Scheduled interviews for one interviewer on a calendar date — used to detect double-booking on
    // create. Overlap is computed in memory (small set) since it depends on each row's duration.
    Task<IReadOnlyList<Interview>> GetScheduledForInterviewerOnDateAsync(Guid interviewerId, DateTime date);

    Task AddAsync(Interview interview);
    Task DeleteAsync(Interview interview);
    Task<InterviewEvaluation?> GetEvaluationByInterviewIdAsync(Guid interviewId);
    Task<InterviewEvaluation?> GetTrackedEvaluationByInterviewIdAsync(Guid interviewId);
    Task AddEvaluationAsync(InterviewEvaluation evaluation);
}
