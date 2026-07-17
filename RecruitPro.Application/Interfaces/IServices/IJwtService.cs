using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IServices
{
    public interface IJwtService
    {
        /// <summary>Signs a short-lived access token that embeds the user's roles and current TokenVersion.</summary>
        string GenerateAccessToken(User user);

        /// <summary>Creates a signed refresh JWT; returns the raw value (given to the client) and its
        /// SHA-256 hash (the only form persisted, so a DB leak never yields usable tokens).</summary>
        (string Raw, string Hash) CreateRefreshToken(User user);

        /// <summary>Hashes a raw refresh token the same way <see cref="CreateRefreshToken"/> does, for DB lookup.</summary>
        string HashRefreshToken(string raw);

        /// <summary>Signs a short-lived (30 min) single-use password-reset token bound to the user's current
        /// TokenVersion. Made single-use by bumping TokenVersion on consumption, which invalidates the token's
        /// embedded version claim.</summary>
        string CreateResetToken(User user);

        /// <summary>Validates a reset token's signature/expiry/type; returns the userId and the embedded
        /// TokenVersion, or null if invalid, expired, or not a reset token.</summary>
        (Guid UserId, int TokenVersion)? ValidateResetToken(string raw);
    }
}
