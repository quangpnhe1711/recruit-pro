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
                .Include(profile => profile.User)
                    .ThenInclude(user => user.Applications)
                        .ThenInclude(application => application.Job)
                            .ThenInclude(job => job.Department)
                .Include(profile => profile.User)
                    .ThenInclude(user => user.Applications)
                        .ThenInclude(application => application.Interviews)
                .Include(profile => profile.Skills)
                .Include(profile => profile.CandidateSkillDetails)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .Include(profile => profile.Projects)
                .Include(profile => profile.Resumes)
                .FirstOrDefaultAsync(profile => profile.UserId == userId);
        }

        public Task<CandidateProfile?> GetByIdAsync(Guid candidateId)
        {
            return _context.CandidateProfiles
                .AsNoTracking()
                .Include(profile => profile.User)
                .Include(profile => profile.Skills)
                .Include(profile => profile.CandidateSkillDetails)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .Include(profile => profile.Projects)
                .Include(profile => profile.Resumes)
                .FirstOrDefaultAsync(profile => profile.Id == candidateId);
        }

        public Task<CandidateProfile?> GetByResumeIdAsync(Guid resumeId)
        {
            return _context.CandidateProfiles
                .Include(profile => profile.User)
                .Include(profile => profile.Skills)
                .Include(profile => profile.CandidateSkillDetails)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .Include(profile => profile.Projects)
                .Include(profile => profile.Resumes)
                .FirstOrDefaultAsync(profile => profile.Resumes.Any(resume => resume.Id == resumeId));
        }

        public Task<CandidateProfile?> GetHrDetailByIdAsync(Guid candidateId)
        {
            return _context.CandidateProfiles
                .AsNoTracking()
                .Include(profile => profile.User)
                    .ThenInclude(user => user.Applications)
                        .ThenInclude(application => application.Job)
                            .ThenInclude(job => job.Department)
                .Include(profile => profile.User)
                    .ThenInclude(user => user.Applications)
                        .ThenInclude(application => application.Interviews)
                .Include(profile => profile.Skills)
                .Include(profile => profile.CandidateSkillDetails)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .Include(profile => profile.Projects)
                .Include(profile => profile.Resumes)
                .FirstOrDefaultAsync(profile => profile.Id == candidateId);
        }

        public async Task<(IReadOnlyList<CandidateProfile> Candidates, int Total)> GetPagedAsync(int page, int pageSize, string? keyword)
        {
            IQueryable<CandidateProfile> query = _context.CandidateProfiles
                .AsNoTracking()
                .Include(profile => profile.User)
                    .ThenInclude(user => user.Applications)
                .Include(profile => profile.Resumes)
                .Include(profile => profile.Skills)
                .Include(profile => profile.CandidateSkillDetails)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string loweredKeyword = keyword.Trim().ToLowerInvariant();
                query = query.Where(profile => profile.User.FullName.ToLower().Contains(loweredKeyword) || profile.User.Email.ToLower().Contains(loweredKeyword));
            }

            query = query.Where(profile =>
                profile.User.Applications.Any() ||
                (!string.IsNullOrWhiteSpace(profile.ResumeUrl)
                 && !string.IsNullOrWhiteSpace(profile.CurrentPosition)
                 && profile.Skills.Any()));

            int total = await query.CountAsync();
            List<CandidateProfile> candidates = await query
                .OrderBy(profile => profile.User.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (candidates, total);
        }

        public Task<CandidateProfile?> GetFirstAsync()
        {
            return _context.CandidateProfiles
                .AsNoTracking()
                .Include(profile => profile.User)
                    .ThenInclude(user => user.Applications)
                        .ThenInclude(application => application.Job)
                .Include(profile => profile.Resumes)
                .Include(profile => profile.Skills)
                .Include(profile => profile.CandidateSkillDetails)
                .OrderBy(profile => profile.User.FullName)
                .FirstOrDefaultAsync();
        }

        public Task UpdateAsync(CandidateProfile profile)
        {
            var entry = _context.Entry(profile);
            if (entry.State == EntityState.Detached)
            {
                _context.CandidateProfiles.Attach(profile);
                entry = _context.Entry(profile);
            }

            if (entry.State == EntityState.Unchanged)
            {
                entry.State = EntityState.Modified;
            }

            return Task.CompletedTask;
        }
    }
}
