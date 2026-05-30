namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    public required string Value { get; init; }
}
