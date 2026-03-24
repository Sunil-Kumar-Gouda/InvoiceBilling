using InvoiceBilling.Infrastructure.Standalone;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvoiceBilling.Api.Tests.Standalone;

public sealed class LocalFileInvoicePdfStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalFileInvoicePdfStorage _storage;

    public LocalFileInvoicePdfStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"InvoiceBilling_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _storage = CreateStorage(_tempDir);
    }

    [Fact]
    public void Constructor_creates_base_directory_if_it_does_not_exist()
    {
        var newDir = Path.Combine(_tempDir, "nested", "storage");
        Assert.False(Directory.Exists(newDir));

        _ = CreateStorage(newDir);

        Assert.True(Directory.Exists(newDir));
    }

    [Fact]
    public async Task TryDownloadAsync_returns_null_when_file_does_not_exist()
    {
        var result = await _storage.TryDownloadAsync(
            "invoices/nonexistent.pdf", "INV-404", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryDownloadAsync_returns_stream_for_existing_file()
    {
        // Arrange: write a fake PDF file to the expected location.
        var invoiceId = Guid.NewGuid();
        var key = $"invoices/{invoiceId}.pdf";
        var filePath = _storage.ResolveFullPath(key);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var expectedBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF magic bytes
        await File.WriteAllBytesAsync(filePath, expectedBytes);

        // Act
        var result = await _storage.TryDownloadAsync(key, "INV-001", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("application/pdf", result!.ContentType);
        Assert.Equal("INV-001.pdf", result.FileName);

        using var ms = new MemoryStream();
        await result.ContentStream.CopyToAsync(ms);
        result.ContentStream.Dispose();

        Assert.Equal(expectedBytes, ms.ToArray());
    }

    [Fact]
    public void ResolveFullPath_converts_forward_slashes_to_platform_separator()
    {
        var path = _storage.ResolveFullPath("invoices/abc.pdf");

        var expected = Path.Combine(_tempDir, "invoices", "abc.pdf");
        Assert.Equal(expected, path);
    }

    [Fact]
    public void ResolveFullPath_uses_base_directory_as_root()
    {
        var path = _storage.ResolveFullPath("test.pdf");

        Assert.StartsWith(_tempDir, path);
        Assert.Equal(Path.Combine(_tempDir, "test.pdf"), path);
    }

    [Fact]
    public async Task TryDownloadAsync_returns_correct_fileName_from_invoiceNumber()
    {
        var key = "invoices/test.pdf";
        var filePath = _storage.ResolveFullPath(key);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllBytesAsync(filePath, new byte[] { 0x00 });

        var result = await _storage.TryDownloadAsync(key, "GOLD-2026-0042", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("GOLD-2026-0042.pdf", result!.FileName);

        result.ContentStream.Dispose();
    }

    [Fact]
    public async Task TryDownloadAsync_returns_octet_stream_for_unknown_extensions()
    {
        var key = "invoices/test.dat";
        var filePath = _storage.ResolveFullPath(key);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllBytesAsync(filePath, new byte[] { 0x00 });

        var result = await _storage.TryDownloadAsync(key, "INV-001", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("application/octet-stream", result!.ContentType);

        result.ContentStream.Dispose();
    }

    [Fact]
    public void BaseDirectory_exposes_resolved_storage_path()
    {
        Assert.Equal(_tempDir, _storage.BaseDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private static LocalFileInvoicePdfStorage CreateStorage(string basePath)
    {
        var options = Options.Create(new StandaloneOptions { PdfStoragePath = basePath });

        // IHostEnvironment.ContentRootPath is only used when PdfStoragePath is relative.
        // Since we pass an absolute path, ContentRootPath can be anything.
        var env = new FakeHostEnvironment { ContentRootPath = Path.GetTempPath() };

        return new LocalFileInvoicePdfStorage(
            options,
            env,
            NullLogger<LocalFileInvoicePdfStorage>.Instance);
    }

    /// <summary>
    /// Minimal <see cref="IHostEnvironment"/> stub. No mocking framework needed.
    /// </summary>
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
    }
}
