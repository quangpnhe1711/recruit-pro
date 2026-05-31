using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories
{
    public class JobRepository : IJobRepository
    {
        private readonly AppDbContext _context;

        public JobRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Job>> GetAllApprovedAsync()
        {
            return await _context.Jobs
                .AsNoTracking()
                .Include(job => job.Department)
                .Include(job => job.JobSkills)
                    .ThenInclude(jobSkill => jobSkill.Skill)
                .Where(job => job.Status == JobStatus.Approved)
                .OrderByDescending(job => job.CreatedAt)
                .ToListAsync();
        }
    }
}
