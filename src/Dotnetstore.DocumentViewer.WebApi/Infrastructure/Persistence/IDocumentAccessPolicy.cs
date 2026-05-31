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
    /// <para>
    /// Intentional leak of the <see cref="Document"/> entity: there is a single consumer
    /// (ListDocumentsEndpoint) and the goal is to keep the filter composable with the
    /// caller's projection. If a second consumer appears with materially different
    /// projection or paging needs, add a typed projection method here instead of
    /// growing parallel <c>.Where(...)</c> chains in callers.
    /// </para>
    /// </summary>
    IQueryable<Document> Viewable(Guid userId, bool isAdmin);
}
