using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class AccessTokenRevocationTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Logout_with_bearer_revokes_access_token_jti_so_subsequent_calls_return_401()
    {
        // Fresh user so we don't disturb other tests that login as the seeded admin.
        var email = $"jti-{Guid.NewGuid():N}@dotnetstore.test";
        const string password = "JtiPass123!";
        await factory.CreateViewerAsync(email, password);

        // Login + verify the access token works.
        var tokens = await LoginAsync(email, password);
        using var authed = factory.CreateAnonymousClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var meBefore = await authed.GetAsync("/auth/me");
        meBefore.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Logout WITH the bearer in the Authorization header — server should also blacklist the jti.
        var logout = await authed.PostAsJsonAsync("/auth/logout", new LogoutRequest(tokens.RefreshToken));
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Same access token now fails validation via the revocation store check.
        var meAfter = await authed.GetAsync("/auth/me");
        meAfter.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_without_bearer_revokes_refresh_only_not_access()
    {
        var email = $"jti-noaccess-{Guid.NewGuid():N}@dotnetstore.test";
        const string password = "JtiPass123!";
        await factory.CreateViewerAsync(email, password);

        var tokens = await LoginAsync(email, password);

        // Logout via the anonymous client (no Authorization header attached).
        using var anon = factory.CreateAnonymousClient();
        var logout = await anon.PostAsJsonAsync("/auth/logout", new LogoutRequest(tokens.RefreshToken));
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Access token still works because we never told the server which jti to blacklist.
        using var authed = factory.CreateAnonymousClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var me = await authed.GetAsync("/auth/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Access_token_after_refresh_rotation_still_works_and_only_the_revoked_one_dies()
    {
        var email = $"jti-rotate-{Guid.NewGuid():N}@dotnetstore.test";
        const string password = "JtiPass123!";
        await factory.CreateViewerAsync(email, password);

        var first = await LoginAsync(email, password);

        // Rotate via /auth/refresh — gets a new access + refresh pair.
        using var anon = factory.CreateAnonymousClient();
        var refreshResp = await anon.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(first.RefreshToken));
        var second = (await refreshResp.Content.ReadFromJsonAsync<TokenResponse>())!;

        // Revoke the FIRST access token via logout-with-bearer.
        using var firstClient = factory.CreateAnonymousClient();
        firstClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        // Pass any refresh token string; whether it matches doesn't affect the access-token revocation path.
        var logout = await firstClient.PostAsJsonAsync("/auth/logout", new LogoutRequest("ignored"));
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // First access token is dead; second still alive.
        var firstMe = await firstClient.GetAsync("/auth/me");
        firstMe.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var secondClient = factory.CreateAnonymousClient();
        secondClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.AccessToken);
        var secondMe = await secondClient.GetAsync("/auth/me");
        secondMe.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<TokenResponse> LoginAsync(string email, string password)
    {
        using var anon = factory.CreateAnonymousClient();
        var login = await anon.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
    }
}
