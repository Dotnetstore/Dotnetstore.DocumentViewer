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
    public const string RenderPageIpBlocked = "RenderPage.IpBlocked";

    public const string DocumentUploaded = "DocumentUploaded";
    public const string DocumentDeleted = "DocumentDeleted";

    public const string AccessGranted = "AccessGranted";
    public const string AccessRevoked = "AccessRevoked";

    public const string UserCreated = "UserCreated";
    public const string UserUpdated = "UserUpdated";
    public const string UserDeleted = "UserDeleted";

    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordReset = "PasswordReset";

    public const string RefreshTokenReuse = "RefreshToken.Reuse";
}
