namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Viewer = "Viewer";

    public static IReadOnlyList<string> All { get; } = [Admin, Viewer];
}
