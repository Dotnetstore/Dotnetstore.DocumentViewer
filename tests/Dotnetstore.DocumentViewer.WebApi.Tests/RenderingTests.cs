using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

/// <summary>
/// These tests exercise the pre-render authorization gates (API key, JWT, signed URL,
/// per-document ACL). They deliberately do NOT verify the rendered PNG bytes — that path
/// requires a real PDF fixture and a renderer dependency the auth-matrix tests don't need.
/// </summary>
[Collection(nameof(DocumentViewerApiCollection))]
public sealed class RenderingTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task RenderPage_without_signature_returns_401()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "RenderPage no sig");

        var response = await admin.GetAsync($"/documents/{doc.Id}/pages/0");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RenderPage_with_tampered_signature_returns_401()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "RenderPage tampered");
        var token = await factory.LoginAsync(DocumentViewerApiFactory.AdminEmail, DocumentViewerApiFactory.AdminPassword);
        var adminId = SubFromJwt(token);

        var signer = factory.Services.GetRequiredService<ISignedUrlService>();
        var signed = signer.Sign(adminId, doc.Id, page: 0);

        // Flip a character in the signature.
        var tampered = signed.Signature[..^1] + (signed.Signature[^1] == 'A' ? 'B' : 'A');
        var url = $"/documents/{doc.Id}/pages/0?exp={signed.ExpiresUnix}&sig={Uri.EscapeDataString(tampered)}";

        var response = await admin.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RenderPage_signed_for_one_user_rejected_when_called_by_another()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "RenderPage cross-user");

        // Grant access to a viewer so the ACL gate would pass for them.
        var email = $"render-x-{Guid.NewGuid():N}@dotnetstore.test";
        var viewer = await factory.CreateViewerAsync(email);
        var grant = await admin.PostAsJsonAsync($"/documents/{doc.Id}/access",
            new Shared.SDK.Dtos.Access.GrantAccessRequest(viewer.Id));
        grant.EnsureSuccessStatusCode();

        // Sign the URL for the VIEWER, then call it as the ADMIN — should fail signature check
        // because HMAC includes userId and the admin's sub doesn't match the signature.
        var signer = factory.Services.GetRequiredService<ISignedUrlService>();
        var signed = signer.Sign(viewer.Id, doc.Id, page: 0);
        var url = $"/documents/{doc.Id}/pages/0?exp={signed.ExpiresUnix}&sig={Uri.EscapeDataString(signed.Signature)}";

        var response = await admin.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ViewerSession_returns_403_for_ungranted_user()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "ViewerSession ungranted");

        var email = $"vs-ungranted-{Guid.NewGuid():N}@dotnetstore.test";
        await factory.CreateViewerAsync(email);
        using var viewer = await factory.CreateViewerClientAsync(email);

        var response = await viewer.GetAsync($"/documents/{doc.Id}/viewer-session");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ViewerSession_returns_404_for_unknown_document()
    {
        using var admin = await factory.CreateAdminClientAsync();

        var response = await admin.GetAsync($"/documents/{Guid.NewGuid()}/viewer-session");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static Guid SubFromJwt(string accessToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var sub = jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        return Guid.Parse(sub);
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
