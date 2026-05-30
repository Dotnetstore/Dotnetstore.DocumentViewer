using Microsoft.Extensions.Options;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;

internal sealed class FileSystemDocumentStorage(IOptions<DocumentStorageOptions> options, TimeProvider clock)
    : IDocumentStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> StoreAsync(Stream content, Guid documentId, string extension, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var relativeDir = Path.Combine(now.Year.ToString("D4"), now.Month.ToString("D2"));
        var relativePath = Path.Combine(relativeDir, $"{documentId:N}{NormalizeExtension(extension)}");
        var absolutePath = Path.Combine(_root, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await using var output = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(output, ct);

        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    public Stream OpenRead(string storagePath)
    {
        var absolutePath = Resolve(storagePath);
        return new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public void Delete(string storagePath)
    {
        var absolutePath = Resolve(storagePath);
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);
    }

    private string Resolve(string storagePath)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, storagePath));
        // Path traversal guard: rebuilt absolute path must remain under _root.
        if (!combined.StartsWith(_root, StringComparison.Ordinal))
            throw new InvalidOperationException("Storage path escaped configured root.");
        return combined;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
        return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
    }
}
