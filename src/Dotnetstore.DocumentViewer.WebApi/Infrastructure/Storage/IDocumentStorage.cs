namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;

public interface IDocumentStorage
{
    Task<string> StoreAsync(Stream content, Guid documentId, string extension, CancellationToken ct);
    Stream OpenRead(string storagePath);
    void Delete(string storagePath);
}
