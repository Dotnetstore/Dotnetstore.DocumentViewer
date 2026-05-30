using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Auth.RefreshToken;

internal sealed class RefreshTokenEndpoint(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IJwtTokenService tokens,
    TimeProvider clock) : Endpoint<RefreshTokenRequest, TokenResponse>
{
    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(RateLimitingOptions.AuthPolicy));
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(RefreshTokenRequest req, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(req.RefreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= clock.GetUtcNow())
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        stored.RevokedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var roles = await userManager.GetRolesAsync(user);
        var issued = await tokens.IssueAsync(user, roles, ct);

        await Send.OkAsync(new TokenResponse(
            issued.AccessToken,
            issued.AccessTokenExpiresAt,
            issued.RefreshToken,
            issued.RefreshTokenExpiresAt), ct);
    }
}
