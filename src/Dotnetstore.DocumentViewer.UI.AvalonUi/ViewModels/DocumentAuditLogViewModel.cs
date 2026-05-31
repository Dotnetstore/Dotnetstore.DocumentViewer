using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Audit;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

public sealed partial class DocumentAuditLogViewModel(IDocumentViewerApiClient api, INavigationService nav) : ViewModelBase
{
    private const int DefaultTake = 200;

    public ObservableCollection<AuditLogEntryDto> Entries { get; } = [];

    [ObservableProperty] private Guid _documentId;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync(Guid documentId, CancellationToken ct = default)
    {
        DocumentId = documentId;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var rows = await api.QueryAuditLogAsync(new AuditLogQuery(DocumentId: documentId, Take: DefaultTake), ct);
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
    private Task Refresh() => LoadAsync(DocumentId);

    [RelayCommand]
    private void Back() => nav.NavigateToDocumentList();
}
