using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Auth.Logout;

/// <summary>
/// Server-side logout: hash the presented refresh token, set RevokedAt on the matching row.
/// AllowAnonymous because the access token may have already expired by the time a client
/// hits Sign Out — possession of the refresh token is the credential. Always returns 204
/// so attackers can't probe which refresh tokens exist.
/// </summary>
internal sealed class LogoutEndpoint(AppDbContext db, IJwtTokenService tokens, TimeProvider clock)
    : Endpoint<LogoutRequest>
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
        await Send.NoContentAsync(ct);
    }
}
