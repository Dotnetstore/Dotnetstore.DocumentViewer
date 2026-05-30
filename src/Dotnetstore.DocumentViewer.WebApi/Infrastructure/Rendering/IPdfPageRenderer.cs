namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Rendering;

public interface IPdfPageRenderer
{
    int GetPageCount(Stream pdf);

    /// <summary>Convenience: rasterize + watermark + encode in one call.</summary>
    byte[] RenderPagePng(Stream pdf, int page, string watermarkText);

    /// <summary>
    /// Rasterize a single PDF page to PNG bytes WITHOUT a watermark. This is the
    /// expensive step and is what the disk cache stores.
    /// </summary>
    byte[] RasterizePagePng(Stream pdf, int page);

    /// <summary>
    /// Decode a previously-rasterized PNG, overlay the per-request watermark, re-encode.
    /// Cheap relative to PDF rasterization, so it runs fresh on every served request.
    /// </summary>
    byte[] ApplyWatermarkPng(byte[] inputPng, string watermarkText);
}
