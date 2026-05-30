using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Audit;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;

namespace Dotnetstore.DocumentViewer.Shared.SDK;

internal sealed class DocumentViewerApiClient(HttpClient http) : IDocumentViewerApiClient
{
    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        await PostJson<LoginRequest, TokenResponse>("/auth/login", request, ct);

    public async Task<TokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default) =>
        await PostJson<RefreshTokenRequest, TokenResponse>("/auth/refresh", request, ct);

    public async Task<MeResponse> MeAsync(CancellationToken ct = default) =>
        await GetJson<MeResponse>("/auth/me", ct);

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("/auth/change-password", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("/auth/logout", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<DocumentDto>> ListDocumentsAsync(CancellationToken ct = default) =>
        await GetJson<List<DocumentDto>>("/documents", ct);

    public async Task<DocumentDto> GetDocumentAsync(Guid id, CancellationToken ct = default) =>
        await GetJson<DocumentDto>($"/documents/{id}", ct);

    public async Task<DocumentDto> UploadDocumentAsync(string title, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(streamContent, "file", fileName);
        if (!string.IsNullOrWhiteSpace(title))
            form.Add(new StringContent(title), "Title");

        using var response = await http.PostAsync("/documents", form, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>(ct))!;
    }

    public async Task<ViewerSessionDto> GetViewerSessionAsync(Guid id, CancellationToken ct = default) =>
        await GetJson<ViewerSessionDto>($"/documents/{id}/viewer-session", ct);

    public async Task<byte[]> DownloadPageAsync(string relativeUrl, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(relativeUrl, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken ct = default) =>
        await GetJson<List<UserDto>>("/users", ct);

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default) =>
        await PostJson<CreateUserRequest, UserDto>("/users", request, ct);

    public async Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync($"/users/{id}", request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>(ct))!;
    }

    public async Task DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"/users/{id}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetUserPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"/users/{id}/reset-password", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DocumentAccessDto> GrantAccessAsync(Guid documentId, GrantAccessRequest request, CancellationToken ct = default) =>
        await PostJson<GrantAccessRequest, DocumentAccessDto>($"/documents/{documentId}/access", request, ct);

    public async Task RevokeAccessAsync(Guid documentId, Guid userId, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"/documents/{documentId}/access/{userId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<DocumentAccessDto>> ListAccessForDocumentAsync(Guid documentId, CancellationToken ct = default) =>
        await GetJson<List<DocumentAccessDto>>($"/documents/{documentId}/access", ct);

    public async Task<IReadOnlyList<AuditLogEntryDto>> QueryAuditLogAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (query.UserId.HasValue) qs.Add($"userId={query.UserId.Value}");
        if (query.DocumentId.HasValue) qs.Add($"documentId={query.DocumentId.Value}");
        if (!string.IsNullOrWhiteSpace(query.Action)) qs.Add($"action={Uri.EscapeDataString(query.Action)}");
        if (query.FromUtc.HasValue) qs.Add($"fromUtc={Uri.EscapeDataString(query.FromUtc.Value.ToString("o"))}");
        if (query.ToUtc.HasValue) qs.Add($"toUtc={Uri.EscapeDataString(query.ToUtc.Value.ToString("o"))}");
        if (query.Take.HasValue) qs.Add($"take={query.Take.Value}");
        var url = "/audit-log" + (qs.Count > 0 ? "?" + string.Join('&', qs) : "");
        return await GetJson<List<AuditLogEntryDto>>(url, ct);
    }

    private async Task<TResponse> GetJson<TResponse>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TResponse>(ct))!;
    }

    private async Task<TResponse> PostJson<TRequest, TResponse>(string path, TRequest request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TResponse>(ct))!;
    }
}
