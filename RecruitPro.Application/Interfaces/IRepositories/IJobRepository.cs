using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories
{
    public interface IJobRepository
    {
        Task<(IReadOnlyList<Job> Jobs, int Total)> GetApprovedPagedAsync(int currentPage, int pageSize);
        Task<(IReadOnlyList<Job> Jobs, int Total)> SearchApprovedAsync(string? keyword, IReadOnlyCollection<string> employmentTypes, IReadOnlyCollection<string> skills, string? sortBy, int currentPage, int pageSize);
        Task<(IReadOnlyList<Job> Jobs, int Total)> GetPagedAsync(string? department, string? approvalStatus, int currentPage, int pageSize);
        Task<int> CountApprovedJobsAsync();
        Task<IReadOnlyList<Job>> GetPendingApprovalJobsAsync(int take);
        Task<Job?> GetByIdAsync(Guid id);
        Task<Job?> GetTrackedByIdAsync(Guid id);
        Task<Department?> GetDepartmentByIdAsync(Guid departmentId);
        Task<Department?> GetDepartmentByNameAsync(string departmentName);
        Task<IReadOnlyList<Department>> GetDepartmentsAsync();
        Task<IReadOnlyList<Skill>> GetSkillsAsync();
        Task AddAsync(Job job);
        Task DeleteAsync(Job job);
        Task<IReadOnlyList<string>> GetAllSkillNamesAsync();
        Task UpdateAsync(Job job);
    }
}
