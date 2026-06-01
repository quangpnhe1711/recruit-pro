using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories
{
    public interface IJobRepository
    {
        Task<(IReadOnlyList<Job> Jobs, int Total)> GetApprovedPagedAsync(int currentPage, int pageSize);
    }
}
