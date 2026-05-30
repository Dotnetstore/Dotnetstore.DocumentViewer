using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;

namespace Dotnetstore.DocumentViewer.Shared.SDK;

public interface IDocumentViewerApiClient
{
    // Auth
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<TokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<MeResponse> MeAsync(CancellationToken ct = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);

    // Documents
    Task<IReadOnlyList<DocumentDto>> ListDocumentsAsync(CancellationToken ct = default);
    Task<DocumentDto> GetDocumentAsync(Guid id, CancellationToken ct = default);
    Task<DocumentDto> UploadDocumentAsync(string title, string fileName, Stream content, string contentType, CancellationToken ct = default);
    Task<ViewerSessionDto> GetViewerSessionAsync(Guid id, CancellationToken ct = default);
    Task<byte[]> DownloadPageAsync(string relativeUrl, CancellationToken ct = default);

    // Users
    Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken ct = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteUserAsync(Guid id, CancellationToken ct = default);

    // Access
    Task<DocumentAccessDto> GrantAccessAsync(Guid documentId, GrantAccessRequest request, CancellationToken ct = default);
    Task RevokeAccessAsync(Guid documentId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentAccessDto>> ListAccessForDocumentAsync(Guid documentId, CancellationToken ct = default);
}
