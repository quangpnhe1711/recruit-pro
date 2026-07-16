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
                .Include(profile => profile.CandidateSkills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .Include(profile => profile.Projects)
                .Include(profile => profile.Resumes)
                .Include(profile => profile.Sections.OrderBy(section => section.DisplayOrder))
                    .ThenInclude(section => section.Items.OrderBy(item => item.DisplayOrder))
                .FirstOrDefaultAsync(profile => profile.UserId == userId);
        }

        public Task<CandidateProfile?> GetByUserIdForUpdateAsync(Guid userId)
        {
            return _context.CandidateProfiles
                .Include(profile => profile.User)
                .Include(profile => profile.Sections)
                    .ThenInclude(section => section.Items)
                .FirstOrDefaultAsync(profile => profile.UserId == userId);
        }

        public Task<CandidateProfile?> GetByIdAsync(Guid candidateId)
        {
            return _context.CandidateProfiles
                .AsNoTracking()
                .Include(profile => profile.User)
                .Include(profile => profile.CandidateSkills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .Include(profile => profile.Projects)
                .Include(profile => profile.Resumes)
                .Include(profile => profile.Sections.OrderBy(section => section.DisplayOrder))
                    .ThenInclude(section => section.Items.OrderBy(item => item.DisplayOrder))
                .FirstOrDefaultAsync(profile => profile.Id == candidateId);
        }

        public Task<CandidateProfile?> GetTrackedByIdAsync(Guid candidateId)
        {
            return _context.CandidateProfiles
                .Include(profile => profile.User)
                .Include(profile => profile.CandidateSkills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .Include(profile => profile.Projects)
                .Include(profile => profile.Resumes)
                .Include(profile => profile.Sections.OrderBy(section => section.DisplayOrder))
                    .ThenInclude(section => section.Items.OrderBy(item => item.DisplayOrder))
                .FirstOrDefaultAsync(profile => profile.Id == candidateId);
        }

        public Task<CandidateProfile?> GetByResumeIdAsync(Guid resumeId)
        {
            return _context.CandidateProfiles
                .Include(profile => profile.User)
                .Include(profile => profile.CandidateSkills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .Include(profile => profile.Projects)
                .Include(profile => profile.Resumes)
                .Include(profile => profile.Sections.OrderBy(section => section.DisplayOrder))
                    .ThenInclude(section => section.Items.OrderBy(item => item.DisplayOrder))
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
                .Include(profile => profile.CandidateSkills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .Include(profile => profile.Projects)
                .Include(profile => profile.Resumes)
                .Include(profile => profile.Sections.OrderBy(section => section.DisplayOrder))
                    .ThenInclude(section => section.Items.OrderBy(item => item.DisplayOrder))
                .FirstOrDefaultAsync(profile => profile.Id == candidateId);
        }

        public async Task<(IReadOnlyList<CandidateProfile> Candidates, int Total)> GetPagedAsync(int page, int pageSize, string? keyword)
        {
            IQueryable<CandidateProfile> query = _context.CandidateProfiles
                .AsNoTracking()
                .Include(profile => profile.User)
                    .ThenInclude(user => user.Applications)
                        .ThenInclude(application => application.Job)
                            .ThenInclude(job => job.Department)
                .Include(profile => profile.Resumes)
                .Include(profile => profile.CandidateSkills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .Include(profile => profile.Sections.OrderBy(section => section.DisplayOrder))
                    .ThenInclude(section => section.Items.OrderBy(item => item.DisplayOrder))
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
                 && profile.CandidateSkills.Any()));

            int total = await query.CountAsync();
            List<CandidateProfile> candidates = await query
                .OrderBy(profile => profile.User.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (candidates, total);
        }

        public async Task<IReadOnlyList<CandidateProfile>> GetAllForSemanticSearchAsync()
        {
            return await _context.CandidateProfiles
                .AsNoTracking()
                .Include(profile => profile.User)
                .Include(profile => profile.CandidateSkills)
                    .ThenInclude(detail => detail.Skill)
                .Include(profile => profile.Projects)
                .Include(profile => profile.Sections.OrderBy(section => section.DisplayOrder))
                    .ThenInclude(section => section.Items.OrderBy(item => item.DisplayOrder))
                .Where(profile =>
                    // candidate_embedding_vector is jsonb — IsNullOrWhiteSpace would emit btrim(jsonb)
                    // which Postgres has no overload for (42883). A null check is the valid jsonb test.
                    profile.CandidateEmbeddingVectorJson != null
                    || !string.IsNullOrWhiteSpace(profile.CurrentPosition)
                    || !string.IsNullOrWhiteSpace(profile.Bio)
                    || profile.CandidateSkills.Any())
                .ToListAsync();
        }

        public Task<CandidateProfile?> GetFirstAsync()
        {
            return _context.CandidateProfiles
                .AsNoTracking()
                .Include(profile => profile.User)
                    .ThenInclude(user => user.Applications)
                        .ThenInclude(application => application.Job)
                .Include(profile => profile.Resumes)
                .Include(profile => profile.CandidateSkills)
                    .ThenInclude(candidateSkill => candidateSkill.Skill)
                .Include(profile => profile.Sections.OrderBy(section => section.DisplayOrder))
                    .ThenInclude(section => section.Items.OrderBy(item => item.DisplayOrder))
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

        public async Task ReplaceProjectsAsync(Guid candidateProfileId, IReadOnlyCollection<CandidateProject> projects)
        {
            List<CandidateProject> trackedProjects = _context.ChangeTracker
                .Entries<CandidateProject>()
                .Where(entry => entry.Entity.CandidateProfileId == candidateProfileId)
                .Select(entry => entry.Entity)
                .ToList();

            foreach (CandidateProject trackedProject in trackedProjects)
            {
                _context.Entry(trackedProject).State = EntityState.Detached;
            }

            await _context.CandidateProjects
                .Where(project => project.CandidateProfileId == candidateProfileId)
                .ExecuteDeleteAsync();

            if (projects.Count == 0)
            {
                return;
            }

            foreach (CandidateProject project in projects)
            {
                project.CandidateProfileId = candidateProfileId;
            }

            await _context.CandidateProjects.AddRangeAsync(projects);
        }

        public async Task ReplaceSkillsAsync(Guid candidateProfileId, IReadOnlyCollection<CandidateSkill> skills)
        {
            List<CandidateSkill> trackedSkills = _context.ChangeTracker
                .Entries<CandidateSkill>()
                .Where(entry => entry.Entity.CandidateId == candidateProfileId)
                .Select(entry => entry.Entity)
                .ToList();

            foreach (CandidateSkill trackedSkill in trackedSkills)
            {
                _context.Entry(trackedSkill).State = EntityState.Detached;
            }

            await _context.CandidateSkills
                .Where(detail => detail.CandidateId == candidateProfileId)
                .ExecuteDeleteAsync();

            if (skills.Count == 0)
            {
                return;
            }

            foreach (CandidateSkill skill in skills)
            {
                skill.CandidateId = candidateProfileId;
            }

            await _context.CandidateSkills.AddRangeAsync(skills);
        }

        public async Task ReplaceSectionsAsync(Guid candidateProfileId, IReadOnlyCollection<CandidateProfileSection> sections)
        {
            List<CandidateProfileSection> trackedSections = _context.ChangeTracker
                .Entries<CandidateProfileSection>()
                .Where(entry => entry.Entity.CandidateProfileId == candidateProfileId)
                .Select(entry => entry.Entity)
                .ToList();

            foreach (CandidateProfileSection trackedSection in trackedSections)
            {
                _context.Entry(trackedSection).State = EntityState.Detached;
            }

            List<CandidateProfileSectionItem> trackedItems = _context.ChangeTracker
                .Entries<CandidateProfileSectionItem>()
                .Where(entry => trackedSections.Select(section => section.Id).Contains(entry.Entity.SectionId))
                .Select(entry => entry.Entity)
                .ToList();

            foreach (CandidateProfileSectionItem trackedItem in trackedItems)
            {
                _context.Entry(trackedItem).State = EntityState.Detached;
            }

            List<Guid> existingSectionIds = await _context.CandidateProfileSections
                .Where(section => section.CandidateProfileId == candidateProfileId)
                .Select(section => section.Id)
                .ToListAsync();

            if (existingSectionIds.Count > 0)
            {
                await _context.CandidateProfileSectionItems
                    .Where(item => existingSectionIds.Contains(item.SectionId))
                    .ExecuteDeleteAsync();
            }

            await _context.CandidateProfileSections
                .Where(section => section.CandidateProfileId == candidateProfileId)
                .ExecuteDeleteAsync();

            if (sections.Count == 0)
            {
                return;
            }

            foreach (CandidateProfileSection section in sections)
            {
                section.CandidateProfileId = candidateProfileId;
                foreach (CandidateProfileSectionItem item in section.Items)
                {
                    item.SectionId = section.Id;
                }
            }

            await _context.CandidateProfileSections.AddRangeAsync(sections);
        }
    }
}
