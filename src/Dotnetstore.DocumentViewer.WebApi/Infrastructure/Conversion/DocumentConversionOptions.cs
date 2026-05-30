namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Conversion;

public sealed class DocumentConversionOptions
{
    public const string SectionName = "DocumentConversion";

    /// <summary>
    /// Path to the LibreOffice binary (`soffice` on Linux/macOS, `soffice.exe` on Windows).
    /// Default assumes `soffice` is on PATH. Override via DocumentConversion:SofficePath.
    /// </summary>
    public string SofficePath { get; init; } = "soffice";

    /// <summary>How often the conversion worker scans for pending DOCX uploads.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Hard cap on a single conversion. Kills the soffice process if exceeded.</summary>
    public TimeSpan ConversionTimeout { get; init; } = TimeSpan.FromMinutes(2);
}
