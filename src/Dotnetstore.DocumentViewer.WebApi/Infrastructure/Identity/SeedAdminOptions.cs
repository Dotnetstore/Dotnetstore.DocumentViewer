namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;

public sealed class SeedAdminOptions
{
    public const string SectionName = "Seed:Admin";

    public string? Email { get; init; }
    public string? Password { get; init; }
    public string DisplayName { get; init; } = "Administrator";
}
