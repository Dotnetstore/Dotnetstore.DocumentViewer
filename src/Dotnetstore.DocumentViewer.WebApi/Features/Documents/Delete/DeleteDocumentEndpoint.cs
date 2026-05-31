using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Auditing;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Caching;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Documents.Delete;

internal sealed class DeleteDocumentEndpoint(
    AppDbContext db,
    IDocumentStorage storage,
    IPageImageCache cache,
    IAuditLogger audit,
    ILogger<DeleteDocumentEndpoint> logger) : EndpointWithoutRequest
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

        User.TryGetUserId(out var actorId);
        var storagePath = document.StoragePath;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Drop ACL rows alongside the document — there's no FK so they'd otherwise orphan.
        // AccessAuditLog rows are kept (DocumentId nullable on the column) so the trail
        // survives a delete; the audit row staged below captures the deletion itself.
        await db.DocumentAccesses.Where(a => a.DocumentId == documentId).ExecuteDeleteAsync(ct);
        db.Documents.Remove(document);
        audit.Add(AuditActions.DocumentDeleted,
            userId: actorId == Guid.Empty ? null : actorId,
            documentId: documentId,
            resultCode: StatusCodes.Status204NoContent,
            ipAddress: ip);
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
