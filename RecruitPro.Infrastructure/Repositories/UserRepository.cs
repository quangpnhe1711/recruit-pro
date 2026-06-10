using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            return _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.CandidateProfile)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public Task<User?> GetTrackedByEmailAsync(string email)
        {
            return _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.CandidateProfile)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<IReadOnlySet<string>> GetExistingEmailsAsync(IEnumerable<string> emails)
        {
            List<string> normalizedEmails = emails
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            List<string> existingEmailList = await _context.Users
                .AsNoTracking()
                .Where(user => normalizedEmails.Contains(user.Email.ToLower()))
                .Select(user => user.Email.ToLower())
                .ToListAsync();

            return existingEmailList.ToHashSet();
        }

        public Task<User?> GetByIdAsync(Guid id)
        {
            return _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.CandidateProfile)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<IReadOnlyList<User>> GetUsersInRolesAsync(params string[] roles)
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(u => u.UserRoles.Any(ur => roles.Contains(ur.Role.Name)))
                .ToListAsync();
        }

        public Task<Role?> GetRoleByNameAsync(string roleName)
        {
            return _context.Roles.FirstOrDefaultAsync(role => role.Name == roleName);
        }

        public Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            return Task.CompletedTask;
        }

        public async Task AddUserRoleAsync(UserRole userRole)
        {
            await _context.UserRoles.AddAsync(userRole);
        }
    }
}
