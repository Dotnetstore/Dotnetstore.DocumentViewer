using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests.Unit;

/// <summary>
/// Unit-level coverage for the path-traversal guard in <see cref="FileSystemDocumentStorage"/>.
/// The guard was originally a plain <c>StartsWith(_root)</c> which lets a sibling-directory
/// name with a shared prefix slip through ("/var/store" vs "/var/storeevil"); these tests
/// pin the corrected GetRelativePath-based check.
/// </summary>
public sealed class FileSystemDocumentStoragePathTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemDocumentStorage _storage;

    public FileSystemDocumentStoragePathTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fsds-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var options = Options.Create(new DocumentStorageOptions { RootPath = _root, MaxBytes = 1024 });
        _storage = new FileSystemDocumentStorage(options, TimeProvider.System);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void OpenRead_throws_for_parent_traversal()
    {
        Should.Throw<InvalidOperationException>(() => _storage.OpenRead("../escape/file.pdf"));
    }

    [Fact]
    public void OpenRead_throws_for_sibling_directory_with_shared_prefix()
    {
        // This is the specific bypass: with root "/.../fsds-tests-XYZ" and a relative
        // path of "../fsds-tests-XYZevil/file.pdf", the combined absolute path used to
        // pass the old StartsWith check despite being a sibling.
        var siblingPath = $"../{Path.GetFileName(_root)}evil/file.pdf";
        Should.Throw<InvalidOperationException>(() => _storage.OpenRead(siblingPath));
    }

    [Fact]
    public void OpenRead_throws_for_rooted_absolute_path()
    {
        var rooted = OperatingSystem.IsWindows() ? @"C:\Windows\notepad.exe" : "/etc/passwd";
        Should.Throw<InvalidOperationException>(() => _storage.OpenRead(rooted));
    }

    [Fact]
    public void OpenRead_accepts_normal_subpath_and_returns_stream()
    {
        // Write a file under the configured root, then open via the relative storage path.
        var subDir = Path.Combine(_root, "2026", "05");
        Directory.CreateDirectory(subDir);
        var file = Path.Combine(subDir, "a.pdf");
        File.WriteAllText(file, "hello");

        using var stream = _storage.OpenRead(Path.Combine("2026", "05", "a.pdf"));
        stream.ShouldNotBeNull();
    }
}
