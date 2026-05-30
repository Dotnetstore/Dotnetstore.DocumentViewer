using System.Net;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class LogoutTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Logout_revokes_refresh_token_so_refresh_returns_401()
    {
        using var anon = factory.CreateAnonymousClient();
        var login = await anon.PostAsJsonAsync("/auth/login",
            new LoginRequest(DocumentViewerApiFactory.AdminEmail, DocumentViewerApiFactory.AdminPassword));
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;

        var logout = await anon.PostAsJsonAsync("/auth/logout", new LogoutRequest(tokens.RefreshToken));
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refresh = await anon.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken));
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_with_unknown_token_returns_204_without_leaking_existence()
    {
        using var anon = factory.CreateAnonymousClient();

        var logout = await anon.PostAsJsonAsync("/auth/logout",
            new LogoutRequest("definitely-not-a-real-refresh-token"));
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_with_empty_token_returns_400()
    {
        using var anon = factory.CreateAnonymousClient();

        var logout = await anon.PostAsJsonAsync("/auth/logout", new LogoutRequest(""));
        logout.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Logout_is_idempotent_for_already_revoked_token()
    {
        using var anon = factory.CreateAnonymousClient();
        var login = await anon.PostAsJsonAsync("/auth/login",
            new LoginRequest(DocumentViewerApiFactory.AdminEmail, DocumentViewerApiFactory.AdminPassword));
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;

        var first = await anon.PostAsJsonAsync("/auth/logout", new LogoutRequest(tokens.RefreshToken));
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var second = await anon.PostAsJsonAsync("/auth/logout", new LogoutRequest(tokens.RefreshToken));
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
