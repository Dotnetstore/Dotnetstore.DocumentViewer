using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Auditing;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Users.Create;

internal sealed class CreateUserEndpoint(UserManager<ApplicationUser> userManager, IAuditLogger audit)
    : Endpoint<CreateUserRequest, UserDto>
{
    public override void Configure()
    {
        Post("/users");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Users"));
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        if (await userManager.FindByEmailAsync(req.Email) is not null)
        {
            AddError(r => r.Email, "A user with this email already exists.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = req.Email,
            Email = req.Email,
            EmailConfirmed = true,
            DisplayName = req.DisplayName,
            MustChangePassword = true,
        };

        var create = await userManager.CreateAsync(user, req.Password);
        if (!create.Succeeded)
        {
            foreach (var err in create.Errors)
                AddError(err.Description);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var addRoles = await userManager.AddToRolesAsync(user, req.Roles);
        if (!addRoles.Succeeded)
        {
            await userManager.DeleteAsync(user);
            foreach (var err in addRoles.Errors)
                AddError(err.Description);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        User.TryGetUserId(out var actorId);
        await audit.LogAsync(AuditActions.UserCreated,
            userId: actorId == Guid.Empty ? null : actorId,
            resultCode: StatusCodes.Status200OK,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct: ct);

        await Send.OkAsync(
            new UserDto(user.Id, user.Email!, user.DisplayName, req.Roles, user.MustChangePassword),
            ct);
    }
}
