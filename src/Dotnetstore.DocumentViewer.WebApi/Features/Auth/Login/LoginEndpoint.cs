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
    IJwtTokenService tokens) : Endpoint<LoginRequest, TokenResponse>
{
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
