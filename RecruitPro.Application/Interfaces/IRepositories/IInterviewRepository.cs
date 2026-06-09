using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface IInterviewRepository
{
    Task<Interview?> GetNextAsync(DateTime fromDate);
    Task<int> CountOnDateAsync(DateTime date);
    Task<(IReadOnlyList<Interview> Interviews, int Total)> GetPagedAsync(int page, int pageSize, string? keyword, InterviewStatus? status, DateTime? startDate, DateTime? endDate);
    Task<Interview?> GetTrackedByIdAsync(Guid interviewId);
    Task AddAsync(Interview interview);
    Task DeleteAsync(Interview interview);
}
