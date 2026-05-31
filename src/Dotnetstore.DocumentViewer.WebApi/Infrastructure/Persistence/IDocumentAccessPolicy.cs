using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;

/// <summary>
/// Centralises the "who can view this document" rule. Used by every read-side endpoint
/// so the admin-bypass-vs-explicit-grant logic exists in exactly one place.
/// </summary>
public interface IDocumentAccessPolicy
{
    Task<bool> CanViewAsync(Guid userId, bool isAdmin, Guid documentId, CancellationToken ct);

    /// <summary>
    /// Returns an <see cref="IQueryable{T}"/> filtered to documents the caller is allowed
    /// to see. Caller continues to project / sort / page in EF — no materialisation here.
    /// </summary>
    IQueryable<Document> Viewable(Guid userId, bool isAdmin);
}
