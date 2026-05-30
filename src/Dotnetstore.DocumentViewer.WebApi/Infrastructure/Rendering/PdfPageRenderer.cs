using System.Runtime.Versioning;
using PDFtoImage;
using SkiaSharp;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Rendering;

// PDFtoImage depends on PDFium native binaries that are shipped for desktop/server
// platforms only. The WebApi is deployed on Windows/Linux/macOS hosts.
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal sealed class PdfPageRenderer : IPdfPageRenderer
{
    private const int RenderDpi = 150;

    // Fully qualified PDFtoImage.Conversion to avoid clashing with the sibling
    // Dotnetstore.DocumentViewer.WebApi.Infrastructure.Conversion namespace.
    public int GetPageCount(Stream pdf) => PDFtoImage.Conversion.GetPageCount(pdf, leaveOpen: true);

    public byte[] RenderPagePng(Stream pdf, int page, string watermarkText)
    {
        var options = new RenderOptions { Dpi = RenderDpi, WithAnnotations = true, WithFormFill = false };
        using var bitmap = PDFtoImage.Conversion.ToImage(pdf, page: page, leaveOpen: true, password: null, options: options);

        DrawWatermark(bitmap, watermarkText);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, quality: 90);
        return data.ToArray();
    }

    private static void DrawWatermark(SKBitmap bitmap, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        using var canvas = new SKCanvas(bitmap);
        using var font = new SKFont { Size = Math.Max(28f, bitmap.Width * 0.025f) };
        using var paint = new SKPaint { Color = new SKColor(220, 0, 0, 90), IsAntialias = true };

        var step = (int)(bitmap.Height * 0.18f);
        canvas.Save();
        canvas.RotateDegrees(-30, bitmap.Width / 2f, bitmap.Height / 2f);
        for (var y = -bitmap.Height; y < bitmap.Height * 2; y += step)
        {
            canvas.DrawText(text, bitmap.Width / 2f, y, SKTextAlign.Center, font, paint);
        }
        canvas.Restore();
    }
}
