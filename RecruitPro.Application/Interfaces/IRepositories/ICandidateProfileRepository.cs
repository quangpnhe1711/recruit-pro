namespace RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;

public interface ICandidateProfileRepository
{
    Task<CandidateProfile> SaveAsync(CandidateProfile profile);
    Task<CandidateProfile?> GetByUserIdAsync(Guid userId);
    Task<CandidateProfile?> GetByIdAsync(Guid candidateId);
    Task UpdateAsync(CandidateProfile profile);
}
