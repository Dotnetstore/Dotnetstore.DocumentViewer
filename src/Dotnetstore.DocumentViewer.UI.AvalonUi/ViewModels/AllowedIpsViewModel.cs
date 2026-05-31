using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

public sealed partial class AllowedIpsViewModel(IDocumentViewerApiClient api, INavigationService nav) : ViewModelBase
{
    public ObservableCollection<AllowedIpDto> Entries { get; } = [];

    [ObservableProperty] private Guid _documentId;
    [ObservableProperty] private string? _newCidr;
    [ObservableProperty] private string? _newDescription;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync(Guid documentId, CancellationToken ct = default)
    {
        DocumentId = documentId;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var rows = await api.ListAllowedIpsAsync(documentId, ct);
            Entries.Clear();
            foreach (var r in rows) Entries.Add(r);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Add()
    {
        if (DocumentId == Guid.Empty || string.IsNullOrWhiteSpace(NewCidr)) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var added = await api.AddAllowedIpAsync(DocumentId, new AddAllowedIpRequest(NewCidr.Trim(), NewDescription));
            // Add idempotent — replace by id if it came back as an existing row.
            var existing = Entries.FirstOrDefault(e => e.Id == added.Id);
            if (existing is not null) Entries[Entries.IndexOf(existing)] = added;
            else Entries.Insert(0, added);
            NewCidr = null;
            NewDescription = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Remove(AllowedIpDto? entry)
    {
        if (entry is null || DocumentId == Guid.Empty) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await api.RemoveAllowedIpAsync(DocumentId, entry.Id);
            Entries.Remove(entry);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync(DocumentId);

    [RelayCommand]
    private void Back() => nav.NavigateToDocumentList();
}
