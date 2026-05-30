using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class ResetPasswordTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Admin_resets_viewer_password_and_forces_must_change_on_next_login()
    {
        var email = $"reset-{Guid.NewGuid():N}@dotnetstore.test";
        const string original = "OrigPass123!";
        const string replacement = "ResetPass789!";

        await factory.CreateViewerAsync(email, original);

        // First reset MustChangePassword to false by changing the password through the user themselves
        // (CreateViewerAsync seeded MustChangePassword=true already, so do a self change to flip it).
        var firstToken = await factory.LoginAsync(email, original);
        using var viewer = factory.CreateAnonymousClient();
        viewer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        var selfChange = await viewer.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest(original, "SelfChosen456!"));
        selfChange.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Sanity: after self-change, MustChangePassword is now false.
        var meBefore = (await viewer.GetFromJsonAsync<MeResponse>("/auth/me"))!;
        meBefore.MustChangePassword.ShouldBeFalse();

        // Admin resets the password.
        using var admin = await factory.CreateAdminClientAsync();
        var reset = await admin.PostAsJsonAsync($"/users/{meBefore.Id}/reset-password",
            new ResetPasswordRequest(replacement));
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Login with the SelfChosen password should now fail.
        using var anon = factory.CreateAnonymousClient();
        var oldLogin = await anon.PostAsJsonAsync("/auth/login", new LoginRequest(email, "SelfChosen456!"));
        oldLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Login with the admin-set password should succeed AND MustChangePassword should be true again.
        var newAccess = await factory.LoginAsync(email, replacement);
        using var afterReset = factory.CreateAnonymousClient();
        afterReset.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newAccess);
        var meAfter = (await afterReset.GetFromJsonAsync<MeResponse>("/auth/me"))!;
        meAfter.MustChangePassword.ShouldBeTrue();
    }

    [Fact]
    public async Task Viewer_cannot_reset_other_users_password()
    {
        var email = $"reset-forbidden-{Guid.NewGuid():N}@dotnetstore.test";
        var viewer = await factory.CreateViewerAsync(email);
        using var viewerClient = await factory.CreateViewerClientAsync(email);

        var response = await viewerClient.PostAsJsonAsync($"/users/{viewer.Id}/reset-password",
            new ResetPasswordRequest("Whatever123!"));
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reset_for_unknown_user_returns_404()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var response = await admin.PostAsJsonAsync($"/users/{Guid.NewGuid()}/reset-password",
            new ResetPasswordRequest("Whatever123!"));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
