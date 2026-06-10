using RecruitPro.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.Interfaces.IRepositories
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetTrackedByEmailAsync(string email);
        Task<IReadOnlySet<string>> GetExistingEmailsAsync(IEnumerable<string> emails);

        Task<User?> GetByIdAsync(Guid id);

        Task<IReadOnlyList<User>> GetUsersInRolesAsync(params string[] roles);
        Task<Role?> GetRoleByNameAsync(string roleName);

        Task AddAsync(User user);
        Task AddUserRoleAsync(UserRole userRole);

        Task UpdateAsync(User user);

    }
}
