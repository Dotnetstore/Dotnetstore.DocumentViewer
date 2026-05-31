using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Views;

public partial class UploadDocumentView : UserControl
{
    private const string PdfMime = "application/pdf";
    private const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public UploadDocumentView()
    {
        InitializeComponent();
    }

    // Code-behind owns the StorageProvider call (it needs the TopLevel). On selection we
    // hand the file metadata + a delayed stream factory to the viewmodel — the actual
    // file handle is only opened when Upload runs.
    private async void OnPickFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not UploadDocumentViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select a document to upload",
            FileTypeFilter =
            [
                new FilePickerFileType("PDF document") { Patterns = ["*.pdf"], MimeTypes = [PdfMime] },
                new FilePickerFileType("Word document") { Patterns = ["*.docx"], MimeTypes = [DocxMime] },
            ],
        });
        if (files.Count == 0) return;

        var file = files[0];
        var props = await file.GetBasicPropertiesAsync();
        var contentType = MimeForName(file.Name);

        vm.SetFile(
            file.Name,
            contentType,
            (long)(props.Size ?? 0UL),
            openStreamAsync: async () => await file.OpenReadAsync());
    }

    private static string MimeForName(string name)
    {
        var ext = Path.GetExtension(name);
        return ext.Equals(".docx", StringComparison.OrdinalIgnoreCase) ? DocxMime : PdfMime;
    }
}
