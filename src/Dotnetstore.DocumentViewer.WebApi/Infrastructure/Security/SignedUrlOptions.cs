namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

public sealed class SignedUrlOptions
{
    public const string SectionName = "SignedUrl";

    public required string SigningKey { get; init; }
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromSeconds(60);
}
