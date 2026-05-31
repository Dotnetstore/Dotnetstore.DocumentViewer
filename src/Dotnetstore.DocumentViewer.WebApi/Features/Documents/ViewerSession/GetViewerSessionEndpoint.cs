using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Rendering;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Documents.ViewerSession;

internal sealed class GetViewerSessionEndpoint(
    AppDbContext db,
    IDocumentStorage storage,
    IPdfPageRenderer renderer,
    IDocumentAccessPolicy accessPolicy,
    IDocumentIpPolicy ipPolicy,
    ISignedUrlService signer) : EndpointWithoutRequest<ViewerSessionDto>
{
    public override void Configure()
    {
        Get("/documents/{id:guid}/viewer-session");
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

        var document = await db.Documents.SingleOrDefaultAsync(d => d.Id == documentId, ct);
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

        // Deny early so we don't mint signed URLs that would just 403 on every page render.
        if (!await ipPolicy.IsAllowedAsync(documentId, User.IsInRole(RoleNames.Admin),
                HttpContext.Connection.RemoteIpAddress, ct))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (document.PageCount == 0)
        {
            await using var pdf = storage.OpenRead(document.StoragePath);
            document.PageCount = renderer.GetPageCount(pdf);
            await db.SaveChangesAsync(ct);
        }

        var pages = new List<SignedPageUrlDto>(document.PageCount);
        for (var i = 0; i < document.PageCount; i++)
        {
            var signed = signer.Sign(userId, document.Id, i);
            var url = $"/documents/{document.Id}/pages/{i}?exp={signed.ExpiresUnix}&sig={Uri.EscapeDataString(signed.Signature)}";
            pages.Add(new SignedPageUrlDto(i, url, DateTimeOffset.FromUnixTimeSeconds(signed.ExpiresUnix)));
        }

        var dto = new DocumentDto(
            document.Id, document.Title, document.OriginalFileName, document.ContentType,
            document.PageCount, document.Status, document.UploadedById, document.UploadedAtUtc);

        await Send.OkAsync(new ViewerSessionDto(dto, pages), ct);
    }
}
