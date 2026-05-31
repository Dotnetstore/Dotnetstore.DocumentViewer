using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Auth.Login;

internal sealed class LoginEndpoint(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signIn,
    IPasswordHasher<ApplicationUser> hasher,
    IJwtTokenService tokens) : Endpoint<LoginRequest, TokenResponse>
{
    // Pre-computed dummy hash used to equalise the wall-clock time between the
    // "user exists" and "user does not exist" paths. Without this, an unauthenticated
    // attacker can enumerate accounts by measuring login latency (~100 ms vs ~1 ms).
    private static readonly string DummyHash =
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(),
            "dummy-password-for-timing-equalisation");

    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting(RateLimitingOptions.AuthPolicy));
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user is null)
        {
            // Discarded result — we only need the CPU cost to match the real verification
            // path so timing doesn't leak whether the email matches a known user.
            _ = hasher.VerifyHashedPassword(new ApplicationUser(), DummyHash, req.Password);
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var check = await signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var roles = await userManager.GetRolesAsync(user);
        var issued = await tokens.IssueAsync(user, roles, ct);
        await Send.OkAsync(new TokenResponse(
            issued.AccessToken,
            issued.AccessTokenExpiresAt,
            issued.RefreshToken,
            issued.RefreshTokenExpiresAt), ct);
    }
}
