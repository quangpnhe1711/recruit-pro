using RecruitPro.Domain.Entities;
using JobApplication = RecruitPro.Domain.Entities.Application;

namespace RecruitPro.Application.Interfaces.IRepositories
{
    public interface IJobRepository
    {
        Task<(IReadOnlyList<Job> Jobs, int Total)> GetApprovedPagedAsync(int currentPage, int pageSize);
        Task<(IReadOnlyList<Job> Jobs, int Total)> SearchApprovedAsync(string? keyword, IReadOnlyCollection<string> employmentTypes, IReadOnlyCollection<string> skills, string? sortBy, int currentPage, int pageSize);
        Task<(IReadOnlyList<Job> Jobs, int Total)> GetPagedAsync(string? department, string? approvalStatus, int currentPage, int pageSize);
        Task<Job?> GetByIdAsync(Guid id);
        Task<(IReadOnlyList<JobApplication> Applications, int Total)> GetJobApplicationsAsync(Guid jobId, int currentPage, int pageSize);
        Task<IReadOnlyList<JobApplication>> GetApplicationsByJobIdAsync(Guid jobId);
        Task<IReadOnlyList<JobApplication>> GetApplicationsByUserIdAsync(Guid userId);
        Task<IReadOnlyList<JobApplication>> GetRecentApplicationsByJobIdAsync(Guid jobId, int take);
        Task<bool> CandidateAlreadyAppliedAsync(Guid userId, Guid jobId);
        Task AddApplicationAsync(JobApplication application);
        Task UpdateApplicationAsync(JobApplication application);
        Task AddAsync(Job job);
        Task DeleteAsync(Job job);
        Task<IReadOnlyList<string>> GetAllSkillNamesAsync();
        Task UpdateAsync(Job job);
    }
}
