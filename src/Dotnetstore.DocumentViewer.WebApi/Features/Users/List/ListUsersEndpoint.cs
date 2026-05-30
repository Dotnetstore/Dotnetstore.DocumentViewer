using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Users.List;

internal sealed class ListUsersEndpoint(AppDbContext db) : EndpointWithoutRequest<IReadOnlyList<UserDto>>
{
    public override void Configure()
    {
        Get("/users");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Users"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await (
            from u in db.Users.AsNoTracking()
            orderby u.Email
            select new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.MustChangePassword,
                Roles = (
                    from ur in db.UserRoles
                    join r in db.Roles on ur.RoleId equals r.Id
                    where ur.UserId == u.Id
                    select r.Name!).ToList()
            })
            .ToListAsync(ct);

        var dtos = rows
            .Select(r => new UserDto(r.Id, r.Email ?? string.Empty, r.DisplayName, r.Roles, r.MustChangePassword))
            .ToList();

        await Send.OkAsync(dtos, ct);
    }
}
