using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class MustChangePasswordEnforcementTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Flagged_viewer_is_blocked_from_documents_endpoint()
    {
        var (client, _) = await CreateFlaggedViewerClientAsync();
        var response = await client.GetAsync("/documents");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Flagged_viewer_is_blocked_from_users_endpoint_with_same_403()
    {
        var (client, _) = await CreateFlaggedViewerClientAsync();
        // /users would be 403 anyway because viewers can't manage users; the point here
        // is that the MCP guard returns first, so the request never reaches the role check.
        var response = await client.GetAsync("/users");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Flagged_viewer_can_still_call_me_and_change_password()
    {
        var (client, pwd) = await CreateFlaggedViewerClientAsync();

        var me = await client.GetAsync("/auth/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);

        var change = await client.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest(pwd, "NewerPass789!"));
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task After_change_then_refresh_user_is_unblocked()
    {
        var (client, pwd) = await CreateFlaggedViewerClientAsync();
        const string newPwd = "Unblock123!";

        // Change password — the mcp=1 access token still works for change-password (allowlisted).
        var change = await client.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest(pwd, newPwd));
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The CURRENT access token still carries mcp=1 — so /documents is still blocked
        // until the user picks up a fresh token via login or refresh. Verify the gate stays.
        var blockedBeforeRefresh = await client.GetAsync("/documents");
        blockedBeforeRefresh.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Re-login with the new password → new JWT with mcp=0 → /documents unblocked.
        var token = await factory.LoginAsync(EmailFor(client), newPwd);
        using var unblocked = factory.CreateAnonymousClient();
        unblocked.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var docs = await unblocked.GetAsync("/documents");
        docs.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- helpers ---------------------------------------------------------

    // Map each test-built client back to the viewer email so we can re-login later.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<HttpClient, string> EmailByClient = new();

    private async Task<(HttpClient Client, string Password)> CreateFlaggedViewerClientAsync()
    {
        var email = $"mcp-{Guid.NewGuid():N}@dotnetstore.test";
        const string pwd = "ViewerPass123!";
        await factory.CreateViewerAsync(email, pwd, mustChangePassword: true);
        var token = await factory.LoginAsync(email, pwd);
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        EmailByClient[client] = email;
        return (client, pwd);
    }

    private static string EmailFor(HttpClient client) =>
        EmailByClient.TryGetValue(client, out var email) ? email : throw new InvalidOperationException("Client not registered.");
}
