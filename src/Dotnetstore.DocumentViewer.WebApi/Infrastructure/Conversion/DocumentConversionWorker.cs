using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Rendering;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Conversion;

/// <summary>
/// Polls for documents in Status=Converting, runs them through <see cref="IDocumentConverter"/>,
/// replaces the on-disk file with the produced PDF, fills in PageCount, and flips Status=Ready
/// (or Failed on any error). Single-tenant + single-process — no work queue, no locking. Phase C
/// scope.
/// </summary>
internal sealed class DocumentConversionWorker(
    IServiceProvider services,
    IOptions<DocumentConversionOptions> options,
    ILogger<DocumentConversionWorker> logger) : BackgroundService
{
    private readonly TimeSpan _pollInterval = options.Value.PollInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DocumentConversionWorker started (poll {Interval}).", _pollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DocumentConversionWorker iteration failed.");
            }

            try { await Task.Delay(_pollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessNextAsync(CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IDocumentStorage>();
        var converter = scope.ServiceProvider.GetRequiredService<IDocumentConverter>();
        var renderer = scope.ServiceProvider.GetService<IPdfPageRenderer>();

        var doc = await db.Documents
            .Where(d => d.Status == DocumentStatus.Converting)
            .OrderBy(d => d.UploadedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (doc is null) return;

        logger.LogInformation("Converting document {DocumentId} ({FileName}).", doc.Id, doc.OriginalFileName);

        try
        {
            byte[] pdfBytes;
            await using (var input = storage.OpenRead(doc.StoragePath))
            {
                pdfBytes = await converter.ConvertToPdfAsync(input, doc.OriginalFileName, ct);
            }

            // Store the produced PDF under a fresh id-derived path, then point the document at it.
            string newStoragePath;
            await using (var pdfStream = new MemoryStream(pdfBytes))
            {
                newStoragePath = await storage.StoreAsync(pdfStream, doc.Id, ".pdf", ct);
            }

            // Best-effort: backfill PageCount immediately for the freshly produced PDF.
            int pageCount = 0;
            if (renderer is not null)
            {
                try
                {
                    await using var produced = storage.OpenRead(newStoragePath);
                    pageCount = renderer.GetPageCount(produced);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read page count post-conversion for {DocumentId}.", doc.Id);
                }
            }

            // Remove the original DOCX (storage path always differs because StoreAsync uses
            // a fresh id-derived name; defensive check just in case).
            if (!string.Equals(doc.StoragePath, newStoragePath, StringComparison.Ordinal))
            {
                try { storage.Delete(doc.StoragePath); }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to delete original DOCX for {DocumentId}.", doc.Id); }
            }

            doc.StoragePath = newStoragePath;
            doc.ContentType = "application/pdf";
            doc.Status = DocumentStatus.Ready;
            if (pageCount > 0) doc.PageCount = pageCount;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Converted document {DocumentId} → {PageCount} page(s).", doc.Id, pageCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Conversion failed for document {DocumentId}.", doc.Id);
            doc.Status = DocumentStatus.Failed;
            await db.SaveChangesAsync(ct);
        }
    }
}
