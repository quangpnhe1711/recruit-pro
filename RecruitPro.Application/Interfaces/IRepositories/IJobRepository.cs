using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories
{
    public interface IJobRepository
    {
        Task<IReadOnlyList<Job>> GetAllApprovedAsync();
    }
}
