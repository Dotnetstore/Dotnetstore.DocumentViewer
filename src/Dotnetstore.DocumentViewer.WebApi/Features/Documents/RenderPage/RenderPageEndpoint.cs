using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
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

        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(expRaw) || !long.TryParse(expRaw, out var exp) || string.IsNullOrWhiteSpace(sig))
        {
            await AuditAsync(db, "RenderPage.BadSignature", userId, documentId, page, StatusCodes.Status401Unauthorized, ip, ct);
            await Send.UnauthorizedAsync(ct);
            return;
        }

        if (!signer.Verify(userId, documentId, page, exp, sig))
        {
            await AuditAsync(db, "RenderPage.BadSignature", userId, documentId, page, StatusCodes.Status401Unauthorized, ip, ct);
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var document = await db.Documents.SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null)
        {
            await AuditAsync(db, "RenderPage.NotFound", userId, documentId, page, StatusCodes.Status404NotFound, ip, ct);
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!User.IsInRole(RoleNames.Admin))
        {
            var hasAccess = await db.DocumentAccesses
                .AnyAsync(a => a.DocumentId == documentId && a.UserId == userId, ct);
            if (!hasAccess)
            {
                await AuditAsync(db, "RenderPage.Forbidden", userId, documentId, page, StatusCodes.Status403Forbidden, ip, ct);
                await Send.ForbiddenAsync(ct);
                return;
            }
        }

        if (page < 0 || (document.PageCount > 0 && page >= document.PageCount))
        {
            await AuditAsync(db, "RenderPage.OutOfRange", userId, documentId, page, StatusCodes.Status404NotFound, ip, ct);
            await Send.NotFoundAsync(ct);
            return;
        }

        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? "unknown";
        var watermark = $"{email}  -  {ip ?? "?"}  -  {clock.GetUtcNow():yyyy-MM-dd HH:mm:ss} UTC";

        byte[] png;
        await using (var pdf = storage.OpenRead(document.StoragePath))
        {
            png = renderer.RenderPagePng(pdf, page, watermark);
        }

        await AuditAsync(db, "RenderPage", userId, documentId, page, StatusCodes.Status200OK, ip, ct);

        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        HttpContext.Response.ContentType = "image/png";
        HttpContext.Response.Headers.CacheControl = "no-store";
        await HttpContext.Response.Body.WriteAsync(png, ct);
    }

    private async Task AuditAsync(AppDbContext db, string action, Guid? userId, Guid? documentId, int? page,
        int resultCode, string? ip, CancellationToken ct)
    {
        db.AccessAuditLogs.Add(new AccessAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentId = documentId,
            PageNumber = page,
            Action = action,
            ResultCode = resultCode,
            IpAddress = ip,
            AtUtc = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }
}
