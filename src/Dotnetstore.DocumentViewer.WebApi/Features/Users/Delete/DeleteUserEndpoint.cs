using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Auditing;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Users.Delete;

internal sealed class DeleteUserEndpoint(UserManager<ApplicationUser> userManager, IAuditLogger audit)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/users/{id:guid}");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Users"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        if (User.TryGetUserId(out var callerId) && callerId == id)
        {
            AddError("You cannot delete your own account.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Prevent removing the last admin account.
        if (await userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            var admins = await userManager.GetUsersInRoleAsync(RoleNames.Admin);
            if (admins.Count <= 1)
            {
                AddError("Cannot delete the last admin account.");
                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
            }
        }

        var delete = await userManager.DeleteAsync(user);
        if (!delete.Succeeded)
        {
            foreach (var err in delete.Errors) AddError(err.Description);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        await audit.LogAsync(AuditActions.UserDeleted,
            userId: User.TryGetUserId(out var actorId) ? actorId : null,
            resultCode: StatusCodes.Status204NoContent,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct: ct);

        await Send.NoContentAsync(ct);
    }
}
