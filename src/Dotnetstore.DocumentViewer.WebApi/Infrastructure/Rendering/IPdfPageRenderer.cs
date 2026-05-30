namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Rendering;

public interface IPdfPageRenderer
{
    int GetPageCount(Stream pdf);
    byte[] RenderPagePng(Stream pdf, int page, string watermarkText);
}
