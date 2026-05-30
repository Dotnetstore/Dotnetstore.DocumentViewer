using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Users.ResetPassword;

internal sealed class ResetPasswordEndpoint(UserManager<ApplicationUser> userManager)
    : Endpoint<ResetPasswordRequest>
{
    public override void Configure()
    {
        Post("/users/{id:guid}/reset-password");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Users"));
    }

    public override async Task HandleAsync(ResetPasswordRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Don't use a reset-token flow here — the admin endpoint already has authorization
        // and we'd otherwise need to register token providers just to immediately consume them.
        if (await userManager.HasPasswordAsync(user))
        {
            var remove = await userManager.RemovePasswordAsync(user);
            if (!remove.Succeeded)
            {
                foreach (var err in remove.Errors) AddError(err.Description);
                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
            }
        }
        var add = await userManager.AddPasswordAsync(user, req.NewPassword);
        if (!add.Succeeded)
        {
            foreach (var err in add.Errors) AddError(err.Description);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        // Force the target user to change the admin-set password on their next login.
        user.MustChangePassword = true;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var err in update.Errors) AddError(err.Description);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
