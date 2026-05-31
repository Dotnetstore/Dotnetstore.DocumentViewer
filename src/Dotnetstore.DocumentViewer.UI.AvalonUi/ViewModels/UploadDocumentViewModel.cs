using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

/// <summary>
/// Admin upload flow. The view's code-behind opens a file picker, then calls
/// <see cref="SetFile"/> with a stream factory so this viewmodel stays MVVM-pure
/// and trivially testable — no IStorageProvider dependency, no Avalonia types.
/// </summary>
public sealed partial class UploadDocumentViewModel(
    IDocumentViewerApiClient api,
    INavigationService nav) : ViewModelBase
{
    private Func<Task<Stream>>? _openStreamAsync;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _selectedFileName = string.Empty;
    [ObservableProperty] private string _selectedContentType = string.Empty;
    [ObservableProperty] private long _selectedFileSize;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    public bool HasSelectedFile => _openStreamAsync is not null;

    /// <summary>
    /// Called by the view after the user picks a file. The factory is invoked at Upload
    /// time so a file handle isn't held open while the user fills in the form.
    /// </summary>
    public void SetFile(string fileName, string contentType, long size, Func<Task<Stream>> openStreamAsync)
    {
        SelectedFileName = fileName;
        SelectedContentType = contentType;
        SelectedFileSize = size;
        _openStreamAsync = openStreamAsync;
        if (string.IsNullOrWhiteSpace(Title))
            Title = Path.GetFileNameWithoutExtension(fileName);
        OnPropertyChanged(nameof(HasSelectedFile));
        UploadCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearFile()
    {
        _openStreamAsync = null;
        SelectedFileName = string.Empty;
        SelectedContentType = string.Empty;
        SelectedFileSize = 0;
        OnPropertyChanged(nameof(HasSelectedFile));
        UploadCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task Upload()
    {
        if (_openStreamAsync is null) return;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            await using var stream = await _openStreamAsync();
            var dto = await api.UploadDocumentAsync(Title, SelectedFileName, stream, SelectedContentType);
            StatusMessage = $"Uploaded \"{dto.Title}\" (status: {dto.Status}).";
            ClearFileCommand.Execute(null);
            Title = string.Empty;
            nav.NavigateToDocumentList();
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
    private void Cancel() => nav.NavigateToDocumentList();

    private bool CanUpload() => !IsBusy && HasSelectedFile && !string.IsNullOrWhiteSpace(Title);

    partial void OnTitleChanged(string value) => UploadCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => UploadCommand.NotifyCanExecuteChanged();
}
