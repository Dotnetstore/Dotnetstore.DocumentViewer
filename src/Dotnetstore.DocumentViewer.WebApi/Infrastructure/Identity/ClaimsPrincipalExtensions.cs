using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the authenticated user id from the JWT <c>sub</c> claim, falling back to the
    /// classic <c>ClaimTypes.NameIdentifier</c> mapping. Returns false (and Guid.Empty) when
    /// the claim is missing or not a valid Guid — the caller decides whether that's a 401
    /// (most endpoints) or a "skip the audit user-id" (delete flows that already gate on role).
    /// </summary>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    /// <summary>Reads the JWT <c>email</c> claim, returning the supplied fallback when absent.</summary>
    public static string GetEmail(this ClaimsPrincipal principal, string fallback = "unknown") =>
        principal.FindFirstValue(JwtRegisteredClaimNames.Email)
        ?? principal.FindFirstValue(ClaimTypes.Email)
        ?? fallback;
}
