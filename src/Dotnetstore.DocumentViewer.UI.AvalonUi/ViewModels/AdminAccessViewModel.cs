using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

public sealed partial class AdminAccessViewModel(IDocumentViewerApiClient api) : ViewModelBase
{
    public ObservableCollection<DocumentDto> Documents { get; } = [];
    public ObservableCollection<UserDto> AvailableUsers { get; } = [];
    public ObservableCollection<DocumentAccessDto> Grants { get; } = [];

    [ObservableProperty] private DocumentDto? _selectedDocument;
    [ObservableProperty] private UserDto? _userToGrant;
    [ObservableProperty] private DocumentAccessDto? _selectedGrant;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var docs = await api.ListDocumentsAsync(ct);
            Documents.Clear();
            foreach (var d in docs) Documents.Add(d);

            var users = await api.ListUsersAsync(ct);
            AvailableUsers.Clear();
            foreach (var u in users) AvailableUsers.Add(u);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    partial void OnSelectedDocumentChanged(DocumentDto? value)
    {
        Grants.Clear();
        if (value is null) return;
        _ = LoadGrantsAsync(value.Id);
    }

    private async Task LoadGrantsAsync(Guid documentId, CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var rows = await api.ListAccessForDocumentAsync(documentId, ct);
            Grants.Clear();
            foreach (var g in rows) Grants.Add(g);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Grant()
    {
        if (SelectedDocument is null || UserToGrant is null) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var grant = await api.GrantAccessAsync(SelectedDocument.Id, new GrantAccessRequest(UserToGrant.Id));
            // Replace any existing row for the same user (idempotent grant), else add.
            var existing = Grants.FirstOrDefault(g => g.UserId == grant.UserId);
            if (existing is not null) Grants[Grants.IndexOf(existing)] = grant;
            else Grants.Add(grant);
            UserToGrant = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Revoke()
    {
        if (SelectedDocument is null || SelectedGrant is null) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await api.RevokeAccessAsync(SelectedDocument.Id, SelectedGrant.UserId);
            Grants.Remove(SelectedGrant);
            SelectedGrant = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();
}
