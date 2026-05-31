using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Auth.ChangePassword;

internal sealed class ChangePasswordEndpoint(UserManager<ApplicationUser> userManager) : Endpoint<ChangePasswordRequest>
{
    public override void Configure()
    {
        Post("/auth/change-password");
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
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

        var result = await userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors) AddError(err.Description);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await userManager.UpdateAsync(user);
        }

        await Send.NoContentAsync(ct);
    }
}
