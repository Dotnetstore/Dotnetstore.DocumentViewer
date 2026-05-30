namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Caching;

/// <summary>
/// Disk-backed cache of UN-watermarked rasterized page PNGs. The expensive PDF→PNG
/// step is cached; the per-user watermark is applied fresh on every render so each
/// served image still carries the requesting user's email/ip/timestamp.
/// </summary>
public interface IPageImageCache
{
    Task<byte[]?> TryReadAsync(Guid documentId, int page, CancellationToken ct);
    Task WriteAsync(Guid documentId, int page, byte[] pngBytes, CancellationToken ct);
    void Invalidate(Guid documentId);

    /// <summary>Resolved absolute root path; useful for the eviction worker to scan.</summary>
    string RootPath { get; }
}
