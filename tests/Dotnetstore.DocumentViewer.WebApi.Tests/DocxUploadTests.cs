using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class DocxUploadTests(DocumentViewerApiFactory factory)
{
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    [Fact]
    public async Task Admin_can_upload_docx_with_status_converting()
    {
        using var admin = await factory.CreateAdminClientAsync();

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(DocumentViewerApiFactory.FakeDocxBytes("docx-upload"));
        content.Headers.ContentType = new MediaTypeHeaderValue(DocxContentType);
        form.Add(content, "file", "report.docx");
        form.Add(new StringContent("Quarterly report"), "Title");

        var response = await admin.PostAsync("/documents", form);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var dto = (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
        dto.Title.ShouldBe("Quarterly report");
        dto.ContentType.ShouldBe(DocxContentType);
        dto.Status.ShouldBe(DocumentStatus.Converting);
        dto.PageCount.ShouldBe(0);
    }

    [Fact]
    public async Task Upload_rejects_unrecognised_payload()
    {
        using var admin = await factory.CreateAdminClientAsync();

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(DocumentViewerApiFactory.FakeUnsupportedBytes());
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "doc.png");

        var response = await admin.PostAsync("/documents", form);
        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }
}
