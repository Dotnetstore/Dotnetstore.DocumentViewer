using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Caching;

internal sealed class FileSystemPageImageCache : IPageImageCache
{
    public string RootPath { get; }

    public FileSystemPageImageCache(
        IOptions<CacheOptions> cacheOptions,
        IOptions<DocumentStorageOptions> storageOptions)
    {
        var configured = cacheOptions.Value.RootPath;
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetFullPath(storageOptions.Value.RootPath), "_cache")
            : Path.GetFullPath(configured);
        Directory.CreateDirectory(root);
        RootPath = root;
    }

    public async Task<byte[]?> TryReadAsync(Guid documentId, int page, CancellationToken ct)
    {
        var path = ResolvePath(documentId, page);
        if (!File.Exists(path)) return null;
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, ct);
            // Touch the file so LRU-by-mtime eviction is accurate.
            try { File.SetLastAccessTimeUtc(path, DateTime.UtcNow); } catch { /* best effort */ }
            return bytes;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    public async Task WriteAsync(Guid documentId, int page, byte[] pngBytes, CancellationToken ct)
    {
        var finalPath = ResolvePath(documentId, page);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        var tempPath = finalPath + ".tmp";
        await File.WriteAllBytesAsync(tempPath, pngBytes, ct);
        // Atomic publish: rename (overwrite if a concurrent writer beat us — both wrote
        // identical bytes, last write wins, no partial reads possible).
        File.Move(tempPath, finalPath, overwrite: true);
    }

    public void Invalidate(Guid documentId)
    {
        var dir = Path.Combine(RootPath, documentId.ToString("N"));
        if (!Directory.Exists(dir)) return;
        try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
    }

    private string ResolvePath(Guid documentId, int page) =>
        Path.Combine(RootPath, documentId.ToString("N"), $"{page}.png");
}
