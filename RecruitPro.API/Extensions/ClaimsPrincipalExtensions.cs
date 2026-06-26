using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RecruitPro.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetCurrentUserId(this ClaimsPrincipal user)
    {
        string userId = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("Thiếu thông tin định danh người dùng.");

        return Guid.Parse(userId);
    }

    public static Guid? TryGetCurrentUserId(this ClaimsPrincipal user)
    {
        string? userId = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : null;
    }

    /// <summary>
    /// The role names carried on the principal (ClaimTypes.Role). Used to authorize ownership-scoped
    /// actions (e.g. SystemAdmin override) inside the service layer without a second DB lookup.
    /// </summary>
    public static IReadOnlyCollection<string> GetRoles(this ClaimsPrincipal user)
    {
        return user.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
}
