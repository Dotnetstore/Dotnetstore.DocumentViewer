using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Documents.GetMetadata;

internal sealed class GetDocumentMetadataEndpoint(
    AppDbContext db,
    IDocumentAccessPolicy accessPolicy)
    : EndpointWithoutRequest<DocumentDto>
{
    public override void Configure()
    {
        Get("/documents/{id:guid}");
        Description(b => b.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var documentId = Route<Guid>("id");

        if (!User.TryGetUserId(out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var document = await db.Documents.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!await accessPolicy.CanViewAsync(userId, User.IsInRole(RoleNames.Admin), documentId, ct))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        await Send.OkAsync(
            new DocumentDto(
                document.Id, document.Title, document.OriginalFileName, document.ContentType,
                document.PageCount, document.Status, document.UploadedById, document.UploadedAtUtc),
            ct);
    }
}
