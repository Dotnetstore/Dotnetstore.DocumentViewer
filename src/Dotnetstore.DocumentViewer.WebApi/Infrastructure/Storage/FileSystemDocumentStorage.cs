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
        var relative = Path.GetRelativePath(_root, combined);
        // StartsWith(_root) was unsafe: with _root="/var/x", a combined of "/var/xevil/y"
        // shares the prefix but is a sibling directory. GetRelativePath emits ".." when
        // the target is outside _root, and a rooted path when on a different volume —
        // either is a rejection. (See dotnet/runtime#41487 for the canonical pattern.)
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("Storage path escaped configured root.");
        return combined;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
        return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
    }
}
