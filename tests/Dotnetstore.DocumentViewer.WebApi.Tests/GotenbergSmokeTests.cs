using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

/// <summary>
/// Opt-in end-to-end test that exercises the full DOCX → Gotenberg → PDF → ready
/// pipeline against a real Gotenberg container. Set the env var
/// <c>RUN_GOTENBERG_TESTS=1</c> to enable. Skipped by default because it pulls a
/// ~600 MB image on first run and depends on Docker.
///
/// The fixture is instantiated *inside* the test body (not via IClassFixture) so a
/// skipped test pays zero container-startup cost — instantiating GotenbergSmokeFactory
/// at class level would still spin Gotenberg + Postgres even if the test was skipped.
/// </summary>
public sealed class GotenbergSmokeTests
{
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static bool IsEnabled => string.Equals(
        Environment.GetEnvironmentVariable("RUN_GOTENBERG_TESTS"), "1", StringComparison.Ordinal);

    [Fact(Skip = "Opt-in. Set RUN_GOTENBERG_TESTS=1 to enable.", SkipUnless = nameof(IsEnabled))]
    public async Task Docx_upload_is_converted_to_pdf_and_marked_ready()
    {
        await using var factory = new GotenbergSmokeFactory();
        await factory.InitializeAsync();

        using var admin = await factory.CreateAdminClientAsync();

        var docxBytes = BuildMinimalDocx("Hello from the Gotenberg smoke test.");

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(docxBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(DocxContentType);
        form.Add(content, "file", "smoke.docx");
        form.Add(new StringContent("E2E smoke"), "Title");

        var upload = await admin.PostAsync("/documents", form);
        upload.EnsureSuccessStatusCode();
        var uploaded = (await upload.Content.ReadFromJsonAsync<DocumentDto>())!;
        uploaded.Status.ShouldBe(DocumentStatus.Converting);

        // Poll until the worker flips status. PollInterval is 1 s in the fixture, so
        // ~2 s is typical on a warm Gotenberg. Give it 2 min for first-pull / cold cache.
        var deadline = Stopwatch.StartNew();
        DocumentDto current = uploaded;
        while (deadline.Elapsed < TimeSpan.FromMinutes(2))
        {
            current = (await admin.GetFromJsonAsync<DocumentDto>($"/documents/{uploaded.Id}"))!;
            if (current.Status == DocumentStatus.Ready) break;
            current.Status.ShouldNotBe(DocumentStatus.Failed,
                $"Conversion failed; document state: {current}");
            await Task.Delay(500);
        }

        current.Status.ShouldBe(DocumentStatus.Ready);
        current.ContentType.ShouldBe("application/pdf");
        current.PageCount.ShouldBeGreaterThan(0);
    }

    private static byte[] BuildMinimalDocx(string body)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, autoSave: true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(body)))));
            main.Document.Save();
        }
        return ms.ToArray();
    }
}
