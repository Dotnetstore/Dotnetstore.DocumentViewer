using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Auditing;

internal sealed class AuditLogger(AppDbContext db, TimeProvider clock) : IAuditLogger
{
    public void Add(
        string action,
        Guid? userId = null,
        Guid? documentId = null,
        int? pageNumber = null,
        int resultCode = 0,
        string? ipAddress = null)
    {
        db.AccessAuditLogs.Add(new AccessAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentId = documentId,
            PageNumber = pageNumber,
            Action = action,
            ResultCode = resultCode,
            IpAddress = ipAddress,
            AtUtc = clock.GetUtcNow(),
        });
    }

    public async Task LogAsync(
        string action,
        Guid? userId = null,
        Guid? documentId = null,
        int? pageNumber = null,
        int resultCode = 0,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        Add(action, userId, documentId, pageNumber, resultCode, ipAddress);
        await db.SaveChangesAsync(ct);
    }
}
