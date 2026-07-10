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

        public Task<User?> GetByUsernameAsync(string username)
        {
            return _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.CandidateProfile)
                .FirstOrDefaultAsync(u => u.Username == username);
        }

        public Task<User?> GetByEmailOrUsernameAsync(string identifier)
        {
            return _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.CandidateProfile)
                .FirstOrDefaultAsync(u => u.Email == identifier || u.Username == identifier);
        }

        public Task<User?> GetTrackedByEmailAsync(string email)
        {
            return _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.CandidateProfile)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public Task<User?> GetTrackedByEmailOrUsernameAsync(string identifier)
        {
            return _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.CandidateProfile)
                .FirstOrDefaultAsync(u => u.Email == identifier || u.Username == identifier);
        }

        public Task<User?> GetTrackedByIdAsync(Guid id)
        {
            return _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.CandidateProfile)
                .FirstOrDefaultAsync(u => u.Id == id);
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

        public async Task<IReadOnlySet<string>> GetExistingUsernamesAsync(IEnumerable<string> usernames)
        {
            List<string> normalizedUsernames = usernames
                .Where(username => !string.IsNullOrWhiteSpace(username))
                .Select(username => username.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            List<string> existingUsernameList = await _context.Users
                .AsNoTracking()
                .Where(user => normalizedUsernames.Contains(user.Username.ToLower()))
                .Select(user => user.Username.ToLower())
                .ToListAsync();

            return existingUsernameList.ToHashSet();
        }

        public Task<bool> ExistsByEmailAsync(string email, Guid? excludedUserId = null)
        {
            IQueryable<User> query = _context.Users.Where(user => user.Email.ToLower() == email.ToLower());
            if (excludedUserId.HasValue)
            {
                query = query.Where(user => user.Id != excludedUserId.Value);
            }

            return query.AnyAsync();
        }

        public Task<bool> ExistsByUsernameAsync(string username, Guid? excludedUserId = null)
        {
            IQueryable<User> query = _context.Users.Where(user => user.Username.ToLower() == username.ToLower());
            if (excludedUserId.HasValue)
            {
                query = query.Where(user => user.Id != excludedUserId.Value);
            }

            return query.AnyAsync();
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

        public async Task<(string? Status, int TokenVersion)?> GetAuthSnapshotAsync(Guid id)
        {
            var snapshot = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new { u.Status, u.TokenVersion })
                .FirstOrDefaultAsync();

            return snapshot == null ? null : (snapshot.Status, snapshot.TokenVersion);
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
            var entry = _context.Entry(user);
            if (entry.State == EntityState.Detached)
            {
                _context.Users.Attach(user);
                entry = _context.Entry(user);
            }

            if (entry.State == EntityState.Unchanged)
            {
                entry.State = EntityState.Modified;
            }

            return Task.CompletedTask;
        }

        public async Task AddUserRoleAsync(UserRole userRole)
        {
            await _context.UserRoles.AddAsync(userRole);
        }
    }
}
