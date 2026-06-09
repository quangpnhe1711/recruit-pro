using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface ICandidateProfileRepository
{
    Task<CandidateProfile> SaveAsync(CandidateProfile profile);
    Task<CandidateProfile?> GetByUserIdAsync(Guid userId);
    Task<CandidateProfile?> GetByIdAsync(Guid candidateId);
    Task<(IReadOnlyList<CandidateProfile> Candidates, int Total)> GetPagedAsync(int page, int pageSize, string? keyword);
    Task<CandidateProfile?> GetFirstAsync();
    Task UpdateAsync(CandidateProfile profile);
}
