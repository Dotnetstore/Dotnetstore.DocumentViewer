using System.IdentityModel.Tokens.Jwt;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Auth.Logout;

/// <summary>
/// Server-side logout: revokes the presented refresh token AND — if the caller also
/// included its current access token in the Authorization header — blacklists that
/// jti so the access token can't be used for the remainder of its 15-min life either.
/// AllowAnonymous because the access token may have already expired by the time a
/// client hits Sign Out — possession of the refresh token is the credential. Always
/// returns 204 so attackers can't probe which refresh tokens exist.
/// </summary>
internal sealed class LogoutEndpoint(
    AppDbContext db,
    IJwtTokenService tokens,
    IAccessTokenRevocationStore revocation,
    TimeProvider clock) : Endpoint<LogoutRequest>
{
    public override void Configure()
    {
        Post("/auth/logout");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(RateLimitingOptions.AuthPolicy));
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(LogoutRequest req, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(req.RefreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }

        // Best-effort: if the caller sent their current access token too, blacklist
        // its jti so the still-valid bearer goes dead instead of lingering for its
        // remaining lifetime. Malformed/expired tokens are silently ignored.
        await TryRevokeAccessTokenAsync(ct);

        await Send.NoContentAsync(ct);
    }

    private async Task TryRevokeAccessTokenAsync(CancellationToken ct)
    {
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is null || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return;

        var rawToken = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(rawToken)) return;

        JwtSecurityToken jwt;
        try { jwt = new JwtSecurityTokenHandler().ReadJwtToken(rawToken); }
        catch { return; }

        var jti = jwt.Id;
        if (string.IsNullOrEmpty(jti)) return;
        if (jwt.ValidTo <= clock.GetUtcNow().UtcDateTime) return;

        Guid.TryParse(jwt.Subject, out var userId);
        await revocation.RevokeAsync(jti, userId, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero), ct);
    }
}
