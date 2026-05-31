using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;

internal sealed class DocumentAccessPolicy(AppDbContext db) : IDocumentAccessPolicy
{
    public Task<bool> CanViewAsync(Guid userId, bool isAdmin, Guid documentId, CancellationToken ct)
    {
        if (isAdmin) return Task.FromResult(true);
        return db.DocumentAccesses.AnyAsync(a => a.DocumentId == documentId && a.UserId == userId, ct);
    }

    public IQueryable<Document> Viewable(Guid userId, bool isAdmin)
    {
        var q = db.Documents.AsNoTracking();
        return isAdmin
            ? q
            : q.Where(d => db.DocumentAccesses.Any(a => a.DocumentId == d.Id && a.UserId == userId));
    }
}
