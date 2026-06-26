using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories
{
    public interface IJobRepository
    {
        Task<(IReadOnlyList<Job> Jobs, int Total)> GetApprovedPagedAsync(int currentPage, int pageSize);
        Task<(IReadOnlyList<Job> Jobs, int Total)> SearchApprovedAsync(string? keyword, IReadOnlyCollection<string> employmentTypes, IReadOnlyCollection<string> skills, string? sortBy, int currentPage, int pageSize);
        Task<(IReadOnlyList<Job> Jobs, int Total)> GetPagedAsync(string? department, string? approvalStatus, int currentPage, int pageSize, Guid? createdByUserId = null);
        Task<(IReadOnlyList<Job> Jobs, int Total)> GetPendingApprovalPagedAsync(string? keyword, string? department, int currentPage, int pageSize);
        Task<int> CountApprovedJobsAsync();
        Task<int> CountPendingApprovalJobsAsync();
        Task<IReadOnlyList<Job>> GetPendingApprovalJobsAsync(int take);
        Task<IReadOnlyList<(string DepartmentName, int OpenRoles, string RecruiterName)>> GetDepartmentOpenRoleSnapshotAsync();
        Task<Job?> GetByIdAsync(Guid id);
        Task<Job?> GetTrackedByIdAsync(Guid id);
        Task<Department?> GetDepartmentByIdAsync(Guid departmentId);
        Task<Department?> GetTrackedDepartmentByIdAsync(Guid departmentId);
        Task<Department?> GetDepartmentByNameAsync(string departmentName);
        Task<IReadOnlyList<Department>> GetDepartmentsAsync();
        Task UpdateDepartmentAsync(Department department);
        Task<IReadOnlyList<Skill>> GetSkillsAsync();
        Task<IReadOnlyList<Job>> GetAllApprovedForSemanticSearchAsync();
        Task AddAsync(Job job);
        Task DeleteAsync(Job job);
        Task<IReadOnlyList<string>> GetAllSkillNamesAsync();
        Task UpdateAsync(Job job);
    }
}
