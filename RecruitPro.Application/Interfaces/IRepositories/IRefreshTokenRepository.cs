using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories
{
    public interface IRefreshTokenRepository
    {
        /// <summary>Stages a new refresh token (persisted on the next SaveChanges).</summary>
        Task AddAsync(RefreshToken token);

        /// <summary>Tracked lookup by stored hash, used to validate and rotate on /refresh.</summary>
        Task<RefreshToken?> GetByHashAsync(string tokenHash);

        /// <summary>Stages a delete of a single token (rotation invalidates the used one).</summary>
        void Remove(RefreshToken token);

        /// <summary>Immediately deletes every refresh token for a user — the revoke-all half of account deactivation.</summary>
        Task DeleteAllForUserAsync(Guid userId);
    }
}
