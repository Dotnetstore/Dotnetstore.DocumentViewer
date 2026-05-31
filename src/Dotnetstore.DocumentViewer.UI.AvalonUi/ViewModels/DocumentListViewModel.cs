using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

public sealed partial class DocumentListViewModel(
    IDocumentViewerApiClient api,
    IApiSession session,
    INavigationService nav) : ViewModelBase
{
    public ObservableCollection<DocumentDto> Documents { get; } = [];

    [ObservableProperty]
    private DocumentDto? _selectedDocument;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsAdmin => session.IsAdmin;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var rows = await api.ListDocumentsAsync(ct);
            Documents.Clear();
            foreach (var d in rows)
                Documents.Add(d);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Open(DocumentDto? document)
    {
        if (document is null) return;
        nav.NavigateToDocument(document.Id);
    }

    [RelayCommand]
    private async Task Delete(DocumentDto? document)
    {
        if (document is null) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await api.DeleteDocumentAsync(document.Id);
            Documents.Remove(document);
            if (SelectedDocument?.Id == document.Id) SelectedDocument = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();
}
