namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;

/// <summary>
/// String constants for the <c>Action</c> column on <c>AccessAuditLog</c>. Centralised so
/// typos don't drift between writer (audit logger) and reader (audit query / dashboards).
/// </summary>
public static class AuditActions
{
    public const string RenderPage = "RenderPage";
    public const string RenderPageBadSignature = "RenderPage.BadSignature";
    public const string RenderPageNotFound = "RenderPage.NotFound";
    public const string RenderPageForbidden = "RenderPage.Forbidden";
    public const string RenderPageOutOfRange = "RenderPage.OutOfRange";

    public const string DocumentDeleted = "DocumentDeleted";
}
