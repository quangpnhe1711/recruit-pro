using RecruitPro.Domain.Enums;
using JobApplication = RecruitPro.Domain.Entities.Application;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface IApplicationRepository
{
    Task<int> CountAsync();
    Task<IReadOnlyList<JobApplication>> GetRecentAsync(int take);
    Task<(IReadOnlyList<JobApplication> Applications, int Total)> GetPagedAsync(int page, int pageSize, string? keyword, string? department, ApplicationStatus? status);
    Task<(IReadOnlyList<JobApplication> Applications, int Total)> GetByJobIdAsync(Guid jobId, int currentPage, int pageSize);
    Task<IReadOnlyList<JobApplication>> GetAllByJobIdAsync(Guid jobId);
    Task<IReadOnlyList<JobApplication>> GetRecentByJobIdAsync(Guid jobId, int take);
    Task<IReadOnlyList<JobApplication>> GetByUserIdAsync(Guid userId);
    Task<JobApplication?> GetByIdAsync(Guid applicationId);
    Task<JobApplication?> GetTrackedByIdAsync(Guid applicationId);
    Task<bool> CandidateAlreadyAppliedAsync(Guid userId, Guid jobId);
    Task AddAsync(JobApplication application);
    Task UpdateAsync(JobApplication application);
}
