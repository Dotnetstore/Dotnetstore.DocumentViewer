using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Converters;

/// <summary>
/// Binds an HTTP status-code int to a foreground brush. Anything >= 400 lights up
/// crimson so failed access attempts pop out of the audit log list; everything else
/// inherits the parent style (UnsetValue means "don't touch this property").
/// </summary>
public sealed class HttpStatusColorConverter : IValueConverter
{
    public static readonly HttpStatusColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int code && code >= 400
            ? Brushes.Crimson
            : AvaloniaProperty.UnsetValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
