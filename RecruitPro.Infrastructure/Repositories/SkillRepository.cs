using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories;

public class SkillRepository : ISkillRepository
{
    private readonly AppDbContext _context;

    public SkillRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Skill>> GetByIdsAsync(IReadOnlyCollection<Guid> skillIds)
    {
        if (skillIds.Count == 0)
        {
            return [];
        }

        return await _context.Skills
            .Where(skill => skillIds.Contains(skill.Id))
            .ToListAsync();
    }
}
