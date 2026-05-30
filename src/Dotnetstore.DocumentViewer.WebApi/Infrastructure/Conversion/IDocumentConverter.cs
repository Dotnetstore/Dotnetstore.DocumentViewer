namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Conversion;

public interface IDocumentConverter
{
    /// <summary>
    /// Converts an Office document (typically .docx) to PDF and returns the absolute path
    /// to the produced PDF. The caller is responsible for moving it into final storage.
    /// </summary>
    Task<string> ConvertToPdfAsync(string inputPath, string outputDirectory, CancellationToken ct);
}
