using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Auth.Me;

internal sealed class MeEndpoint(UserManager<ApplicationUser> userManager) : EndpointWithoutRequest<MeResponse>
{
    public override void Configure()
    {
        Get("/auth/me");
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!User.TryGetUserId(out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        // ClientIp lets the UI show the caller what address the server actually sees
        // (post-ForwardedHeaders), which is useful when a per-document IP allow-list
        // is in play and a viewer needs to ask their admin what to whitelist.
        await Send.OkAsync(
            new MeResponse(
                user.Id, user.Email ?? string.Empty, user.DisplayName, roles,
                user.MustChangePassword,
                HttpContext.Connection.RemoteIpAddress?.ToString()),
            ct);
    }
}
