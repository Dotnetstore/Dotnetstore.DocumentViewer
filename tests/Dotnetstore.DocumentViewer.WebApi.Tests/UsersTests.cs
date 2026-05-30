using System.Net;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class UsersTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Admin_can_create_and_list_users()
    {
        using var admin = await factory.CreateAdminClientAsync();

        var email = $"created-{Guid.NewGuid():N}@dotnetstore.test";
        var create = await admin.PostAsJsonAsync("/users",
            new CreateUserRequest(email, "Test Viewer", "ViewerPass123!", [RoleNames.Viewer]));
        create.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await admin.GetFromJsonAsync<List<UserDto>>("/users");
        list.ShouldNotBeNull();
        list.ShouldContain(u => u.Email == email && u.Roles.Contains(RoleNames.Viewer));
    }

    [Fact]
    public async Task Viewer_cannot_list_users()
    {
        var email = $"viewer-list-{Guid.NewGuid():N}@dotnetstore.test";
        await factory.CreateViewerAsync(email);
        using var viewer = await factory.CreateViewerClientAsync(email);

        var response = await viewer.GetAsync("/users");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_cannot_delete_self()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var me = await admin.GetFromJsonAsync<Shared.SDK.Dtos.Auth.MeResponse>("/auth/me");

        var response = await admin.DeleteAsync($"/users/{me!.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // Note: the "cannot delete the last admin" rule in DeleteUserEndpoint is defense-in-depth.
    // It is unreachable behind the "cannot delete self" check (the only path to admins.Count==1
    // is the caller IS that one admin, and the cannot-delete-self rule fires first). Exercising
    // it from a test would require destroying the seeded admin and corrupting fixture state for
    // every subsequent test in the collection. Skipped intentionally.

    [Fact]
    public async Task Update_user_changes_display_name_and_roles()
    {
        var email = $"updatable-{Guid.NewGuid():N}@dotnetstore.test";
        var viewer = await factory.CreateViewerAsync(email);
        using var admin = await factory.CreateAdminClientAsync();

        var update = await admin.PutAsJsonAsync($"/users/{viewer.Id}",
            new UpdateUserRequest("Updated Display", [RoleNames.Admin, RoleNames.Viewer]));
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var refreshed = (await admin.GetFromJsonAsync<List<UserDto>>("/users"))!
            .Single(u => u.Id == viewer.Id);
        refreshed.DisplayName.ShouldBe("Updated Display");
        refreshed.Roles.ShouldContain(RoleNames.Admin);
        refreshed.Roles.ShouldContain(RoleNames.Viewer);
    }

    [Fact]
    public async Task Create_user_with_duplicate_email_returns_409()
    {
        var email = $"dup-{Guid.NewGuid():N}@dotnetstore.test";
        await factory.CreateViewerAsync(email);

        using var admin = await factory.CreateAdminClientAsync();
        var second = await admin.PostAsJsonAsync("/users",
            new CreateUserRequest(email, "Second", "AnotherPass123!", [RoleNames.Viewer]));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
