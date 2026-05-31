using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

/// <summary>
/// Prunes RevokedAccessTokens rows whose ExpiresAt is in the past — once the jti's
/// natural expiry has elapsed, the row no longer changes any validation outcome.
/// Runs hourly; this is housekeeping, not on the hot path.
/// </summary>
internal sealed class RevokedAccessTokenCleanupWorker(
    IServiceProvider services,
    TimeProvider clock,
    ILogger<RevokedAccessTokenCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RevokedAccessTokenCleanupWorker started (interval {Interval}).", Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = clock.GetUtcNow();
                var deleted = await db.RevokedAccessTokens
                    .Where(t => t.ExpiresAt < now)
                    .ExecuteDeleteAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Pruned {Count} expired access-token revocations.", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RevokedAccessTokenCleanupWorker iteration failed.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
