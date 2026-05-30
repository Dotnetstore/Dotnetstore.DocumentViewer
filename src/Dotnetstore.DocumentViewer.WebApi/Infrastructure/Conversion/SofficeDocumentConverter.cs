using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Conversion;

/// <summary>
/// Converts documents by shelling out to LibreOffice's `soffice --headless --convert-to pdf`.
/// LibreOffice must be installed and resolvable via DocumentConversion:SofficePath (defaults to
/// looking it up on PATH). Production deployments typically install libreoffice-core +
/// libreoffice-writer on the WebApi host (or run it in a sidecar container).
/// </summary>
internal sealed class SofficeDocumentConverter(
    IOptions<DocumentConversionOptions> options,
    ILogger<SofficeDocumentConverter> logger) : IDocumentConverter
{
    private readonly DocumentConversionOptions _options = options.Value;

    public async Task<string> ConvertToPdfAsync(string inputPath, string outputDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDirectory);

        var psi = new ProcessStartInfo
        {
            FileName = _options.SofficePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--headless");
        psi.ArgumentList.Add("--nologo");
        psi.ArgumentList.Add("--nofirststartwizard");
        psi.ArgumentList.Add("--convert-to");
        psi.ArgumentList.Add("pdf");
        psi.ArgumentList.Add("--outdir");
        psi.ArgumentList.Add(outputDirectory);
        psi.ArgumentList.Add(inputPath);

        using var process = new Process { StartInfo = psi };
        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.ConversionTimeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"LibreOffice conversion exceeded {_options.ConversionTimeout}.");
        }

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            logger.LogError("soffice exit {ExitCode}. stdout={Stdout}; stderr={Stderr}", process.ExitCode, stdout, stderr);
            throw new InvalidOperationException($"LibreOffice exited with code {process.ExitCode}.");
        }

        var expected = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(inputPath) + ".pdf");
        if (!File.Exists(expected))
            throw new InvalidOperationException($"LibreOffice did not produce expected output at {expected}.");

        return expected;
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { /* best-effort cleanup */ }
    }
}
