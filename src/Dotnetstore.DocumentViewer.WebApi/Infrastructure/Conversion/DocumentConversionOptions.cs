namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Conversion;

public enum DocumentConversionMode
{
    /// <summary>Talk to a Gotenberg container (default, container-friendly).</summary>
    Gotenberg = 0,
    /// <summary>Shell out to a locally-installed LibreOffice binary.</summary>
    Soffice = 1,
}

public sealed class DocumentConversionOptions
{
    public const string SectionName = "DocumentConversion";

    /// <summary>Which converter implementation to register. Default: Gotenberg.</summary>
    public DocumentConversionMode Mode { get; init; } = DocumentConversionMode.Gotenberg;

    /// <summary>How often the conversion worker scans for pending DOCX uploads.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Hard cap on a single conversion. Applies to both Soffice and Gotenberg paths.</summary>
    public TimeSpan ConversionTimeout { get; init; } = TimeSpan.FromMinutes(2);

    public SofficeSettings Soffice { get; init; } = new();
    public GotenbergSettings Gotenberg { get; init; } = new();

    public sealed class SofficeSettings
    {
        /// <summary>Path to the LibreOffice binary. Defaults to `soffice` on PATH.</summary>
        public string SofficePath { get; init; } = "soffice";
    }

    public sealed class GotenbergSettings
    {
        /// <summary>Base address of the Gotenberg HTTP API. Defaults to the Aspire service-discovery name.</summary>
        public string BaseAddress { get; init; } = "http://gotenberg";
    }
}
