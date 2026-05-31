namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Auditing;

/// <summary>
/// Single writer for <c>AccessAuditLog</c> rows. Callers pass the
/// <see cref="Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity.AuditActions"/>
/// constant; everything else (Id + AtUtc stamp) is handled here.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Stages the audit row on the shared <c>AppDbContext</c> WITHOUT calling
    /// <c>SaveChangesAsync</c>. Use this when the calling endpoint already commits
    /// other entity changes in the same transaction — the audit row rides along.
    /// </summary>
    void Add(
        string action,
        Guid? userId = null,
        Guid? documentId = null,
        int? pageNumber = null,
        int resultCode = 0,
        string? ipAddress = null);

    /// <summary>
    /// Adds the audit row and commits it on its own. Use this from read-side
    /// endpoints (render, list, get) that don't otherwise mutate state.
    /// </summary>
    Task LogAsync(
        string action,
        Guid? userId = null,
        Guid? documentId = null,
        int? pageNumber = null,
        int resultCode = 0,
        string? ipAddress = null,
        CancellationToken ct = default);
}
