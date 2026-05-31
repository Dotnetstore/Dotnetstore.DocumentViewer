namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Auditing;

/// <summary>
/// Single writer for <c>AccessAuditLog</c> rows. Callers pass the
/// <see cref="Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity.AuditActions"/>
/// constant; everything else (Id + AtUtc stamp, SaveChanges) is handled here.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        string action,
        Guid? userId = null,
        Guid? documentId = null,
        int? pageNumber = null,
        int resultCode = 0,
        string? ipAddress = null,
        CancellationToken ct = default);
}
