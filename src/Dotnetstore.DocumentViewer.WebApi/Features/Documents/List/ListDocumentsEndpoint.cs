using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Documents.List;

internal sealed class ListDocumentsEndpoint(IDocumentAccessPolicy accessPolicy)
    : EndpointWithoutRequest<IReadOnlyList<DocumentDto>>
{
    public override void Configure()
    {
        Get("/documents");
        Description(b => b.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!User.TryGetUserId(out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var rows = await accessPolicy.Viewable(userId, User.IsInRole(RoleNames.Admin))
            .OrderByDescending(d => d.UploadedAtUtc)
            .Select(d => new DocumentDto(
                d.Id, d.Title, d.OriginalFileName, d.ContentType,
                d.PageCount, d.Status, d.UploadedById, d.UploadedAtUtc))
            .ToListAsync(ct);

        await Send.OkAsync(rows, ct);
    }
}
