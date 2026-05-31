using System.Threading.Tasks;

namespace RecruitPro.Application.Interfaces
{
    public interface IUnitOfWork
    {
        Task BeginTransactionAsync();
        Task<int> SaveChangesAsync();
        Task CommitAsync();
        Task RollbackAsync();
    }
}
