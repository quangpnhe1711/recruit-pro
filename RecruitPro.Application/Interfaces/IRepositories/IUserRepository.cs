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
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByEmailOrUsernameAsync(string identifier);
        Task<User?> GetTrackedByEmailAsync(string email);
        Task<User?> GetTrackedByEmailOrUsernameAsync(string identifier);
        Task<User?> GetTrackedByIdAsync(Guid id);
        Task<IReadOnlySet<string>> GetExistingEmailsAsync(IEnumerable<string> emails);
        Task<IReadOnlySet<string>> GetExistingUsernamesAsync(IEnumerable<string> usernames);
        Task<bool> ExistsByEmailAsync(string email, Guid? excludedUserId = null);
        Task<bool> ExistsByUsernameAsync(string username, Guid? excludedUserId = null);

        Task<User?> GetByIdAsync(Guid id);

        /// <summary>Cheap projection (status + token version) read on every authenticated request to enforce deactivation.</summary>
        Task<(string? Status, int TokenVersion)?> GetAuthSnapshotAsync(Guid id);

        Task<IReadOnlyList<User>> GetUsersInRolesAsync(params string[] roles);
        Task<Role?> GetRoleByNameAsync(string roleName);

        Task AddAsync(User user);
        Task AddUserRoleAsync(UserRole userRole);

        Task UpdateAsync(User user);

    }
}
