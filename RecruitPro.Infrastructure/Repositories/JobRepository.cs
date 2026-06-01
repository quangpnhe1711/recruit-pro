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

        public async Task<(IReadOnlyList<Job> Jobs, int Total)> GetApprovedPagedAsync(int currentPage, int pageSize)
        {
            var query = _context.Jobs
                .AsNoTracking()
                .Include(job => job.Department)
                .Include(job => job.JobSkills)
                    .ThenInclude(jobSkill => jobSkill.Skill)
                .Where(job => job.Status == JobStatus.Approved);

            int total = await query.CountAsync();

            var jobs = await query
                .OrderByDescending(job => job.CreatedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (jobs, total);
        }
    }
}
