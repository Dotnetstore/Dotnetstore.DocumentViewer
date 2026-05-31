using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Auditing;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Caching;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Rendering;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Documents.RenderPage;

internal sealed class RenderPageEndpoint(
    AppDbContext db,
    IDocumentStorage storage,
    IPdfPageRenderer renderer,
    IPageImageCache cache,
    IDocumentAccessPolicy accessPolicy,
    IDocumentIpPolicy ipPolicy,
    IAuditLogger audit,
    ISignedUrlService signer,
    TimeProvider clock) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/documents/{id:guid}/pages/{page:int}");
        Description(b => b.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var documentId = Route<Guid>("id");
        var page = Route<int>("page");
        var expRaw = Query<string?>("exp", isRequired: false);
        var sig = Query<string?>("sig", isRequired: false);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (!User.TryGetUserId(out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(expRaw) || !long.TryParse(expRaw, out var exp) || string.IsNullOrWhiteSpace(sig))
        {
            await FailAndAudit(AuditActions.RenderPageBadSignature, StatusCodes.Status401Unauthorized,
                userId, documentId, page, ip, ct);
            await Send.UnauthorizedAsync(ct);
            return;
        }

        if (!signer.Verify(userId, documentId, page, exp, sig))
        {
            await FailAndAudit(AuditActions.RenderPageBadSignature, StatusCodes.Status401Unauthorized,
                userId, documentId, page, ip, ct);
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var document = await db.Documents.SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null)
        {
            await FailAndAudit(AuditActions.RenderPageNotFound, StatusCodes.Status404NotFound,
                userId, documentId, page, ip, ct);
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!await accessPolicy.CanViewAsync(userId, User.IsInRole(RoleNames.Admin), documentId, ct))
        {
            await FailAndAudit(AuditActions.RenderPageForbidden, StatusCodes.Status403Forbidden,
                userId, documentId, page, ip, ct);
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (!await ipPolicy.IsAllowedAsync(documentId, User.IsInRole(RoleNames.Admin),
                HttpContext.Connection.RemoteIpAddress, ct))
        {
            await FailAndAudit(AuditActions.RenderPageIpBlocked, StatusCodes.Status403Forbidden,
                userId, documentId, page, ip, ct);
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (page < 0 || (document.PageCount > 0 && page >= document.PageCount))
        {
            await FailAndAudit(AuditActions.RenderPageOutOfRange, StatusCodes.Status404NotFound,
                userId, documentId, page, ip, ct);
            await Send.NotFoundAsync(ct);
            return;
        }

        var email = User.GetEmail();
        var watermark = $"{email}  -  {ip ?? "?"}  -  {clock.GetUtcNow():yyyy-MM-dd HH:mm:ss} UTC";

        // Two-step: rasterized (unwatermarked) PNG is cached on disk per (docId, page);
        // the per-request watermark is overlaid fresh so each served image carries the
        // current user's email + ip + UTC timestamp.
        var rasterized = await cache.TryReadAsync(documentId, page, ct);
        if (rasterized is null)
        {
            await using var pdf = storage.OpenRead(document.StoragePath);
            rasterized = renderer.RasterizePagePng(pdf, page);
            await cache.WriteAsync(documentId, page, rasterized, ct);
        }
        var png = renderer.ApplyWatermarkPng(rasterized, watermark);

        await audit.LogAsync(AuditActions.RenderPage, userId, documentId, page,
            StatusCodes.Status200OK, ip, ct);

        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        HttpContext.Response.ContentType = "image/png";
        HttpContext.Response.Headers.CacheControl = "no-store";
        await HttpContext.Response.Body.WriteAsync(png, ct);
    }

    private Task FailAndAudit(string action, int status,
        Guid? userId, Guid? documentId, int? page, string? ip, CancellationToken ct) =>
        audit.LogAsync(action, userId, documentId, page, status, ip, ct);
}
