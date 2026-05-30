using Microsoft.Extensions.Options;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Caching;

/// <summary>
/// Periodically prunes <see cref="IPageImageCache"/> to keep total bytes under
/// CacheOptions.MaxBytes and individual entries below MaxAge. Cheap (one disk scan
/// per interval); no need to hook into reads/writes.
/// </summary>
internal sealed class CacheEvictionWorker(
    IPageImageCache cache,
    IOptions<CacheOptions> options,
    TimeProvider clock,
    ILogger<CacheEvictionWorker> logger) : BackgroundService
{
    private readonly CacheOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "CacheEvictionWorker started (root={Root}, max={MaxMb} MB, maxAge={MaxAge}, interval={Interval}).",
            cache.RootPath, _options.MaxBytes / (1024 * 1024), _options.MaxAge, _options.EvictionInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RunOnce();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cache eviction iteration failed.");
            }

            try { await Task.Delay(_options.EvictionInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void RunOnce()
    {
        if (!Directory.Exists(cache.RootPath)) return;

        var now = clock.GetUtcNow().UtcDateTime;
        var files = new DirectoryInfo(cache.RootPath)
            .EnumerateFiles("*.png", SearchOption.AllDirectories)
            .ToList();

        // Age-bound: drop entries older than MaxAge (by last access).
        long totalBytes = 0;
        var survivors = new List<FileInfo>(files.Count);
        foreach (var fi in files)
        {
            var age = now - fi.LastAccessTimeUtc;
            if (age > _options.MaxAge)
            {
                TryDelete(fi);
                continue;
            }
            totalBytes += fi.Length;
            survivors.Add(fi);
        }

        // Size-bound: if still over limit, drop oldest-accessed first.
        if (totalBytes > _options.MaxBytes)
        {
            survivors.Sort((a, b) => a.LastAccessTimeUtc.CompareTo(b.LastAccessTimeUtc));
            foreach (var fi in survivors)
            {
                if (totalBytes <= _options.MaxBytes) break;
                var size = fi.Length;
                if (TryDelete(fi)) totalBytes -= size;
            }
        }

        // Best-effort: remove empty per-document directories left behind by eviction.
        foreach (var sub in new DirectoryInfo(cache.RootPath).EnumerateDirectories())
        {
            if (!sub.EnumerateFileSystemInfos().Any())
            {
                try { sub.Delete(); } catch { /* best effort */ }
            }
        }
    }

    private bool TryDelete(FileInfo fi)
    {
        try { fi.Delete(); return true; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete cache file {Path}.", fi.FullName);
            return false;
        }
    }
}
