using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Auditing;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Users.Update;

internal sealed class UpdateUserEndpoint(UserManager<ApplicationUser> userManager, IAuditLogger audit)
    : Endpoint<UpdateUserRequest, UserDto>
{
    public override void Configure()
    {
        Put("/users/{id:guid}");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Users"));
    }

    public override async Task HandleAsync(UpdateUserRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        user.DisplayName = req.DisplayName;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var err in update.Errors) AddError(err.Description);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var toRemove = currentRoles.Except(req.Roles).ToList();
        var toAdd = req.Roles.Except(currentRoles).ToList();

        if (toRemove.Count > 0)
        {
            var remove = await userManager.RemoveFromRolesAsync(user, toRemove);
            if (!remove.Succeeded)
            {
                foreach (var err in remove.Errors) AddError(err.Description);
                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
            }
        }
        if (toAdd.Count > 0)
        {
            var add = await userManager.AddToRolesAsync(user, toAdd);
            if (!add.Succeeded)
            {
                foreach (var err in add.Errors) AddError(err.Description);
                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
            }
        }

        User.TryGetUserId(out var actorId);
        await audit.LogAsync(AuditActions.UserUpdated,
            userId: actorId == Guid.Empty ? null : actorId,
            resultCode: StatusCodes.Status200OK,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct: ct);

        await Send.OkAsync(
            new UserDto(user.Id, user.Email!, user.DisplayName, req.Roles, user.MustChangePassword),
            ct);
    }
}
