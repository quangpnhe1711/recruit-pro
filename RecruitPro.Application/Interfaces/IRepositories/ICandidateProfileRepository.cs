using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories
{
    public interface ICandidateProfileRepository
    {
        Task<CandidateProfile> SaveAsync(CandidateProfile profile);
    }
}
