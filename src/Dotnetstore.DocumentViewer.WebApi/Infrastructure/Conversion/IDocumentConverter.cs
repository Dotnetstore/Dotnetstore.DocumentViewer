namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Conversion;

public interface IDocumentConverter
{
    /// <summary>
    /// Converts an Office document (typically .docx) to PDF bytes. The implementation
    /// owns any temp-file management; the caller hands over the source stream and an
    /// indicative filename (extension matters — Gotenberg + LibreOffice both branch on it).
    /// </summary>
    Task<byte[]> ConvertToPdfAsync(Stream input, string sourceFileName, CancellationToken ct);
}
