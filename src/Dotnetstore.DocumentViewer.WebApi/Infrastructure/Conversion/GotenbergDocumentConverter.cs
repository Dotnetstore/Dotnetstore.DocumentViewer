using System.Net.Http.Headers;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Conversion;

/// <summary>
/// Calls a Gotenberg container's LibreOffice route to convert a document to PDF.
/// Gotenberg (https://gotenberg.dev) wraps LibreOffice (and Chromium for HTML→PDF) behind a
/// stable HTTP API. The Aspire AppHost runs the container and Aspire service discovery
/// resolves the named `gotenberg` http endpoint.
/// </summary>
internal sealed class GotenbergDocumentConverter(HttpClient http, ILogger<GotenbergDocumentConverter> logger)
    : IDocumentConverter
{
    private const string ConvertPath = "/forms/libreoffice/convert";

    public async Task<byte[]> ConvertToPdfAsync(Stream input, string sourceFileName, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var part = new StreamContent(input);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        // Gotenberg keys the conversion off the filename extension — keep the original ext.
        form.Add(part, "files", sourceFileName);

        using var response = await http.PostAsync(ConvertPath, form, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Gotenberg returned {Status}. Body: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Gotenberg conversion failed: HTTP {(int)response.StatusCode}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
