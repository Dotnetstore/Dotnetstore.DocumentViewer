namespace Dotnetstore.DocumentViewer.Shared.SDK;

public sealed class ApiClientOptions
{
    public const string SectionName = "DocumentViewerApi";

    public required string BaseAddress { get; init; }
    public required string ApiKey { get; init; }
}
