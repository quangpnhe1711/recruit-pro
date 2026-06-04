using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories
{
    public class CandidateProfileRepository : ICandidateProfileRepository
    {
        private readonly AppDbContext _context;

        public CandidateProfileRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CandidateProfile> SaveAsync(CandidateProfile profile)
        {
            await _context.CandidateProfiles.AddAsync(profile);
            return profile;
        }

        public Task<CandidateProfile?> GetByUserIdAsync(Guid userId)
        {
            return _context.CandidateProfiles
                .Include(x => x.User)
                    .ThenInclude(x => x.Applications)
                        .ThenInclude(x => x.Job)
                            .ThenInclude(x => x.Department)
                .Include(x => x.User)
                    .ThenInclude(x => x.Applications)
                        .ThenInclude(x => x.Interviews)
                .Include(x => x.Skills)
                .FirstOrDefaultAsync(x => x.UserId == userId);
        }

        public Task<CandidateProfile?> GetByIdAsync(Guid candidateId)
        {
            return _context.CandidateProfiles
                .Include(x => x.User)
                .Include(x => x.Skills)
                .FirstOrDefaultAsync(x => x.Id == candidateId);
        }

        public Task UpdateAsync(CandidateProfile profile)
        {
            _context.CandidateProfiles.Update(profile);
            return Task.CompletedTask;
        }
    }
}
