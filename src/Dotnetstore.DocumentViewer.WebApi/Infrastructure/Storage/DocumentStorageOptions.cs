namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;

public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    public required string RootPath { get; init; }
    public long MaxBytes { get; init; } = 100L * 1024 * 1024;
}
