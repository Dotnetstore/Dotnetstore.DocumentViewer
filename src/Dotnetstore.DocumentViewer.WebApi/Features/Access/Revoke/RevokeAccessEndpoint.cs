using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Access.Revoke;

internal sealed class RevokeAccessEndpoint(AppDbContext db) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/documents/{id:guid}/access/{userId:guid}");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Access"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var documentId = Route<Guid>("id");
        var userId = Route<Guid>("userId");

        var access = await db.DocumentAccesses
            .SingleOrDefaultAsync(a => a.DocumentId == documentId && a.UserId == userId, ct);
        if (access is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        db.DocumentAccesses.Remove(access);
        await db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
