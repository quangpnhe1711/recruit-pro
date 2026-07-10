using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly AppDbContext _context;

        public RefreshTokenRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(RefreshToken token)
        {
            await _context.RefreshTokens.AddAsync(token);
        }

        public Task<RefreshToken?> GetByHashAsync(string tokenHash)
        {
            return _context.RefreshTokens.FirstOrDefaultAsync(t => t.Token == tokenHash);
        }

        public void Remove(RefreshToken token)
        {
            _context.RefreshTokens.Remove(token);
        }

        public Task DeleteAllForUserAsync(Guid userId)
        {
            return _context.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync();
        }
    }
}
