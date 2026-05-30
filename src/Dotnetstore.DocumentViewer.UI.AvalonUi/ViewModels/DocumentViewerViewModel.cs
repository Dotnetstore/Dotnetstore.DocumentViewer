using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

public sealed partial class DocumentViewerViewModel(
    IDocumentViewerApiClient api,
    INavigationService nav) : ViewModelBase
{
    public ObservableCollection<PageImage> Pages { get; } = [];

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public async Task LoadAsync(Guid documentId, CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        Pages.Clear();
        try
        {
            var session = await api.GetViewerSessionAsync(documentId, ct);
            Title = session.Document.Title;
            foreach (var page in session.Pages)
            {
                var bytes = await api.DownloadPageAsync(page.Url, ct);
                using var ms = new MemoryStream(bytes);
                var bitmap = new Bitmap(ms);
                Pages.Add(new PageImage(page.Page, bitmap));
            }
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
    private void Back() => nav.NavigateToDocumentList();
}

public sealed record PageImage(int Page, Bitmap Bitmap);
