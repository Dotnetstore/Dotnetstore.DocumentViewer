using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Access.AllowedIps;

internal sealed class RemoveAllowedIpEndpoint(AppDbContext db) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/documents/{id:guid}/allowed-ips/{ipId:guid}");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Access"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var documentId = Route<Guid>("id");
        var ipId = Route<Guid>("ipId");

        var entry = await db.DocumentAllowedIps
            .SingleOrDefaultAsync(a => a.Id == ipId && a.DocumentId == documentId, ct);
        if (entry is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        db.DocumentAllowedIps.Remove(entry);
        await db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
