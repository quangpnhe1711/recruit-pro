using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface ICandidateProfileRepository
{
    Task<CandidateProfile> SaveAsync(CandidateProfile profile);
    Task<CandidateProfile?> GetByUserIdAsync(Guid userId);
    Task<CandidateProfile?> GetByUserIdForUpdateAsync(Guid userId);
    Task<CandidateProfile?> GetByIdAsync(Guid candidateId);
    Task<CandidateProfile?> GetTrackedByIdAsync(Guid candidateId);
    Task<CandidateProfile?> GetByResumeIdAsync(Guid resumeId);
    Task<CandidateProfile?> GetHrDetailByIdAsync(Guid candidateId);
    Task<IReadOnlyList<CandidateProfile>> GetAllForSemanticSearchAsync();
    Task<(IReadOnlyList<CandidateProfile> Candidates, int Total)> GetPagedAsync(int page, int pageSize, string? keyword);
    Task<CandidateProfile?> GetFirstAsync();
    Task UpdateAsync(CandidateProfile profile);
    Task ReplaceProjectsAsync(Guid candidateProfileId, IReadOnlyCollection<CandidateProject> projects);
    Task ReplaceSkillsAsync(Guid candidateProfileId, IReadOnlyCollection<CandidateSkill> skills);
    Task ReplaceSectionsAsync(Guid candidateProfileId, IReadOnlyCollection<CandidateProfileSection> sections);
}
