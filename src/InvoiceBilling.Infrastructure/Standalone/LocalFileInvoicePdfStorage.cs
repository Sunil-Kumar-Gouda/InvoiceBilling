using InvoiceBilling.Application.Common.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceBilling.Infrastructure.Standalone;

/// <summary>
/// Standalone replacement for <see cref="Storage.S3InvoicePdfStorage"/>.
/// Reads invoice PDFs from the local filesystem.
/// </summary>
/// <remarks>
/// The storage key format (<c>invoices/{id}.pdf</c>) is intentionally identical
/// to the S3 key format used in cloud mode. This means:
/// <list type="bullet">
///   <item>The <see cref="Domain.Entities.Invoice.PdfS3Key"/> column works unchanged.</item>
///   <item>Migrating from standalone to cloud only requires copying files to S3.</item>
/// </list>
/// </remarks>
public sealed class LocalFileInvoicePdfStorage : IInvoicePdfStorage
{
    private readonly string _baseDirectory;
    private readonly ILogger<LocalFileInvoicePdfStorage> _logger;

    public LocalFileInvoicePdfStorage(
        IOptions<StandaloneOptions> options,
        IHostEnvironment environment,
        ILogger<LocalFileInvoicePdfStorage> logger)
    {
        _logger = logger;

        var configuredPath = (options.Value.PdfStoragePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(configuredPath))
            configuredPath = "App_Data/invoices";

        _baseDirectory = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(_baseDirectory);

        _logger.LogInformation(
            "LocalFileInvoicePdfStorage initialized. BaseDirectory={BaseDirectory}.",
            _baseDirectory);
    }

    /// <summary>
    /// The resolved base directory for PDF files. Used by <see cref="InvoiceBilling.Api.Background.InProcessPdfWorker"/>
    /// to write PDFs to the same location that this storage reads from.
    /// </summary>
    public string BaseDirectory => _baseDirectory;

    /// <summary>
    /// Resolves the storage key to a local file path and returns a read stream.
    /// </summary>
    public Task<InvoicePdfDownload?> TryDownloadAsync(
        string s3Key,
        string invoiceNumber,
        CancellationToken ct)
    {
        var filePath = ResolveFullPath(s3Key);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("PDF file not found at {FilePath} for key {Key}.", filePath, s3Key);
            return Task.FromResult<InvoicePdfDownload?>(null);
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var contentType = ResolveContentType(filePath);
        var ext = Path.GetExtension(filePath);
        var fileName = string.IsNullOrWhiteSpace(ext) ? invoiceNumber : $"{invoiceNumber}{ext}";

        return Task.FromResult<InvoicePdfDownload?>(
            new InvoicePdfDownload(stream, contentType, fileName));
    }

    /// <summary>
    /// Converts a storage key (e.g. <c>invoices/{id}.pdf</c>) to an absolute local file path.
    /// </summary>
    public string ResolveFullPath(string key)
    {
        var normalized = key.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_baseDirectory, normalized);
    }

    private static string ResolveContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
}
