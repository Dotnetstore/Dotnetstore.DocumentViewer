namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Caching;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Where on disk to keep rasterized page PNGs (unwatermarked). Empty → defaults
    /// to `{DocumentStorage:RootPath}/_cache`.
    /// </summary>
    public string? RootPath { get; init; }

    /// <summary>Hard cap on total cache size before eviction kicks in. Default 1 GB.</summary>
    public long MaxBytes { get; init; } = 1024L * 1024 * 1024;

    /// <summary>Entries older than this are evicted regardless of size. Default 30 days.</summary>
    public TimeSpan MaxAge { get; init; } = TimeSpan.FromDays(30);

    /// <summary>How often the eviction worker runs.</summary>
    public TimeSpan EvictionInterval { get; init; } = TimeSpan.FromHours(1);
}
