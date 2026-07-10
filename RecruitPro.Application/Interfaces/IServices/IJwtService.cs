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

        /// <summary>Creates a new opaque refresh token; returns the raw value (given to the client) and its
        /// SHA-256 hash (the only form persisted, so a DB leak never yields usable tokens).</summary>
        (string Raw, string Hash) CreateRefreshToken();

        /// <summary>Hashes a raw refresh token the same way <see cref="CreateRefreshToken"/> does, for DB lookup.</summary>
        string HashRefreshToken(string raw);
    }
}
