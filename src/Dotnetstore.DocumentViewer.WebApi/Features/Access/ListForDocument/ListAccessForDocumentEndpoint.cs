using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Access.ListForDocument;

internal sealed class ListAccessForDocumentEndpoint(AppDbContext db)
    : EndpointWithoutRequest<IReadOnlyList<DocumentAccessDto>>
{
    public override void Configure()
    {
        Get("/documents/{id:guid}/access");
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

        var rows = await (
            from a in db.DocumentAccesses
            where a.DocumentId == documentId
            join u in db.Users on a.UserId equals u.Id into uu
            from u in uu.DefaultIfEmpty()
            orderby a.GrantedAtUtc descending
            select new DocumentAccessDto(
                a.Id,
                a.DocumentId,
                a.UserId,
                u != null ? u.Email : null,
                a.GrantedById,
                a.GrantedAtUtc))
            .ToListAsync(ct);

        await Send.OkAsync(rows, ct);
    }
}
