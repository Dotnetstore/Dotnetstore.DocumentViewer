using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Access.AllowedIps;

internal sealed class ListAllowedIpsEndpoint(AppDbContext db)
    : EndpointWithoutRequest<IReadOnlyList<AllowedIpDto>>
{
    public override void Configure()
    {
        Get("/documents/{id:guid}/allowed-ips");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Access"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var documentId = Route<Guid>("id");

        var documentExists = await db.Documents.AnyAsync(d => d.Id == documentId, ct);
        if (!documentExists)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var rows = await db.DocumentAllowedIps
            .AsNoTracking()
            .Where(a => a.DocumentId == documentId)
            .OrderByDescending(a => a.AddedAtUtc)
            .Select(a => new AllowedIpDto(a.Id, a.DocumentId, a.Cidr, a.Description, a.AddedById, a.AddedAtUtc))
            .ToListAsync(ct);

        await Send.OkAsync(rows, ct);
    }
}
