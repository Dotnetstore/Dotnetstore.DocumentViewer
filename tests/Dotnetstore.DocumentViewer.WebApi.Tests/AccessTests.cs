using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class AccessTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Grant_list_revoke_round_trip()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "Access RT");
        var email = $"access-rt-{Guid.NewGuid():N}@dotnetstore.test";
        var viewer = await factory.CreateViewerAsync(email);

        var grant = await admin.PostAsJsonAsync($"/documents/{doc.Id}/access",
            new GrantAccessRequest(viewer.Id));
        grant.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = (await grant.Content.ReadFromJsonAsync<DocumentAccessDto>())!;
        dto.UserEmail.ShouldBe(email);

        var list = await admin.GetFromJsonAsync<List<DocumentAccessDto>>($"/documents/{doc.Id}/access");
        list.ShouldNotBeNull();
        list.ShouldContain(a => a.UserId == viewer.Id);

        var revoke = await admin.DeleteAsync($"/documents/{doc.Id}/access/{viewer.Id}");
        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listAfter = await admin.GetFromJsonAsync<List<DocumentAccessDto>>($"/documents/{doc.Id}/access");
        listAfter.ShouldNotBeNull();
        listAfter.ShouldNotContain(a => a.UserId == viewer.Id);
    }

    [Fact]
    public async Task Grant_is_idempotent_returns_existing()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "Idempotent grant");
        var email = $"access-idem-{Guid.NewGuid():N}@dotnetstore.test";
        var viewer = await factory.CreateViewerAsync(email);

        var first = await admin.PostAsJsonAsync($"/documents/{doc.Id}/access",
            new GrantAccessRequest(viewer.Id));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto1 = (await first.Content.ReadFromJsonAsync<DocumentAccessDto>())!;

        var second = await admin.PostAsJsonAsync($"/documents/{doc.Id}/access",
            new GrantAccessRequest(viewer.Id));
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto2 = (await second.Content.ReadFromJsonAsync<DocumentAccessDto>())!;

        dto2.Id.ShouldBe(dto1.Id);
    }

    [Fact]
    public async Task Grant_for_unknown_document_returns_404()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var email = $"access-404-{Guid.NewGuid():N}@dotnetstore.test";
        var viewer = await factory.CreateViewerAsync(email);

        var response = await admin.PostAsJsonAsync($"/documents/{Guid.NewGuid()}/access",
            new GrantAccessRequest(viewer.Id));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Grant_for_unknown_user_returns_400()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "Grant unknown user");

        var response = await admin.PostAsJsonAsync($"/documents/{doc.Id}/access",
            new GrantAccessRequest(Guid.NewGuid()));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Viewer_cannot_grant_access()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "Viewer cannot grant");
        var email = $"access-viewer-{Guid.NewGuid():N}@dotnetstore.test";
        await factory.CreateViewerAsync(email);
        using var viewer = await factory.CreateViewerClientAsync(email);

        var response = await viewer.PostAsJsonAsync($"/documents/{doc.Id}/access",
            new GrantAccessRequest(Guid.NewGuid()));
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<DocumentDto> UploadAsync(HttpClient client, string title)
    {
        using var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(DocumentViewerApiFactory.FakePdfBytes(title));
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", $"{title.Replace(' ', '_')}.pdf");
        form.Add(new StringContent(title), "Title");
        var response = await client.PostAsync("/documents", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
    }
}
