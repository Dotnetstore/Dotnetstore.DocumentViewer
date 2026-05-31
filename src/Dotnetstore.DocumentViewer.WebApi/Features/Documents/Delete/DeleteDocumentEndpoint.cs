using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Caching;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Documents.Delete;

internal sealed class DeleteDocumentEndpoint(
    AppDbContext db,
    IDocumentStorage storage,
    IPageImageCache cache,
    ILogger<DeleteDocumentEndpoint> logger,
    TimeProvider clock) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/documents/{id:guid}");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var documentId = Route<Guid>("id");

        var document = await db.Documents.SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid.TryParse(sub, out var actorId);
        var storagePath = document.StoragePath;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Drop ACL rows alongside the document — there's no FK so they'd otherwise orphan.
        // AccessAuditLog rows are kept (DocumentId nullable on the column) so the trail
        // survives a delete; the audit row written below captures the deletion itself.
        await db.DocumentAccesses.Where(a => a.DocumentId == documentId).ExecuteDeleteAsync(ct);
        db.Documents.Remove(document);
        db.AccessAuditLogs.Add(new AccessAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actorId == Guid.Empty ? null : actorId,
            DocumentId = documentId,
            Action = "DocumentDeleted",
            ResultCode = StatusCodes.Status204NoContent,
            IpAddress = ip,
            AtUtc = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);

        // Best-effort cleanup of side artifacts. Failures here don't undo the DB delete —
        // a missed file or stale cache entry is recoverable; a half-committed delete isn't.
        try { storage.Delete(storagePath); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to delete stored file for {DocumentId}.", documentId); }

        try { cache.Invalidate(documentId); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to invalidate page cache for {DocumentId}.", documentId); }

        await Send.NoContentAsync(ct);
    }
}
