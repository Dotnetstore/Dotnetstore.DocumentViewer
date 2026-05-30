using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class AuthFlowTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Login_returns_tokens_for_seeded_admin()
    {
        using var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(DocumentViewerApiFactory.AdminEmail, DocumentViewerApiFactory.AdminPassword));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        token.ShouldNotBeNull();
        token.AccessToken.ShouldNotBeNullOrEmpty();
        token.RefreshToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        using var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(DocumentViewerApiFactory.AdminEmail, "wrong-password"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_without_api_key_returns_401()
    {
        using var client = factory.CreateBareClient();

        var response = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(DocumentViewerApiFactory.AdminEmail, DocumentViewerApiFactory.AdminPassword));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_rotates_and_revokes_previous()
    {
        using var client = factory.CreateAnonymousClient();
        var login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(DocumentViewerApiFactory.AdminEmail, DocumentViewerApiFactory.AdminPassword));
        var first = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;

        var refresh = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(first.RefreshToken));
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = (await refresh.Content.ReadFromJsonAsync<TokenResponse>())!;

        second.AccessToken.ShouldNotBe(first.AccessToken);
        second.RefreshToken.ShouldNotBe(first.RefreshToken);

        var reused = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(first.RefreshToken));
        reused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_returns_admin_profile_with_must_change_password_flag()
    {
        using var client = await factory.CreateAdminClientAsync();

        var me = await client.GetFromJsonAsync<MeResponse>("/auth/me");

        me.ShouldNotBeNull();
        me.Email.ShouldBe(DocumentViewerApiFactory.AdminEmail);
        me.Roles.ShouldContain("Admin");
        me.MustChangePassword.ShouldBeTrue();
    }

    [Fact]
    public async Task ChangePassword_clears_must_change_flag_and_allows_login_with_new_password()
    {
        // Use a fresh user so we don't disturb the seeded admin for other tests.
        const string email = "changepw@dotnetstore.test";
        const string oldPwd = "OldPass123!";
        const string newPwd = "NewPass456!";
        await factory.CreateViewerAsync(email, oldPwd);

        var initialToken = await factory.LoginAsync(email, oldPwd);
        using var user = factory.CreateAnonymousClient();
        user.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", initialToken);

        var change = await user.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest(oldPwd, newPwd));
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Login with old password fails
        using var anon = factory.CreateAnonymousClient();
        var oldLogin = await anon.PostAsJsonAsync("/auth/login", new LoginRequest(email, oldPwd));
        oldLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Login with new password succeeds and MustChangePassword is cleared
        var newAccess = await factory.LoginAsync(email, newPwd);
        using var afterChange = factory.CreateAnonymousClient();
        afterChange.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newAccess);
        var me = await afterChange.GetFromJsonAsync<MeResponse>("/auth/me");
        me!.MustChangePassword.ShouldBeFalse();
    }
}
