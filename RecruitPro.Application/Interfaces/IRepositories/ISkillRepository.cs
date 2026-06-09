using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface ISkillRepository
{
    Task<IReadOnlyList<Skill>> GetByIdsAsync(IReadOnlyCollection<Guid> skillIds);
}
