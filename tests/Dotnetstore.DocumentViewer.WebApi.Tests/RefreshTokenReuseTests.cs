using System.Net;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Audit;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class RefreshTokenReuseTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_whole_family()
    {
        // Fresh viewer so we don't entangle with the seeded admin's tokens.
        var email = $"reuse-{Guid.NewGuid():N}@dotnetstore.test";
        const string pwd = "ViewerPass123!";
        await factory.CreateViewerAsync(email, pwd);

        using var client = factory.CreateAnonymousClient();
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, pwd));
        var first = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;

        // Legitimate rotation: first → second; first is now revoked in DB.
        var firstRefresh = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(first.RefreshToken));
        firstRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = (await firstRefresh.Content.ReadFromJsonAsync<TokenResponse>())!;

        // Reuse the OLD token (theft signal). Should 401 AND cascade-revoke the new token too.
        var reused = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(first.RefreshToken));
        reused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The legitimate user's current token (issued at the rotation above) is also dead.
        var secondAfterReuse = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(second.RefreshToken));
        secondAfterReuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reuse_writes_a_refreshtokenreuse_audit_row()
    {
        var email = $"reuse-audit-{Guid.NewGuid():N}@dotnetstore.test";
        const string pwd = "ViewerPass123!";
        var viewer = await factory.CreateViewerAsync(email, pwd);

        using var client = factory.CreateAnonymousClient();
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, pwd));
        var first = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        _ = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(first.RefreshToken));
        var reused = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(first.RefreshToken));
        reused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var admin = await factory.CreateAdminClientAsync();
        var rows = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>(
            $"/audit-log?userId={viewer.Id}&action=RefreshToken.Reuse");
        rows.ShouldNotBeNull();
        rows.ShouldContain(r => r.Action == "RefreshToken.Reuse" && r.UserId == viewer.Id);
    }

    [Fact]
    public async Task Unknown_refresh_token_returns_401_without_audit_or_cascade()
    {
        var email = $"reuse-unknown-{Guid.NewGuid():N}@dotnetstore.test";
        const string pwd = "ViewerPass123!";
        var viewer = await factory.CreateViewerAsync(email, pwd);

        // Give the viewer a real active refresh token first; that token must remain valid
        // even though we then probe with a totally unrelated random base64 blob.
        using var client = factory.CreateAnonymousClient();
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, pwd));
        var legit = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;

        var junk = Convert.ToBase64String(Enumerable.Range(0, 64).Select(i => (byte)i).ToArray());
        var probe = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(junk));
        probe.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The legitimate token still refreshes successfully — junk did NOT trigger cascade.
        var legitRefresh = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(legit.RefreshToken));
        legitRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        // And no RefreshToken.Reuse audit row was written for this user.
        using var admin = await factory.CreateAdminClientAsync();
        var rows = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>(
            $"/audit-log?userId={viewer.Id}&action=RefreshToken.Reuse");
        rows.ShouldNotBeNull();
        rows.ShouldNotContain(r => r.UserId == viewer.Id);
    }
}
