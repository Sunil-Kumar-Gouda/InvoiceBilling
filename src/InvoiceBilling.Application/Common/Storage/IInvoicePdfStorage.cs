namespace InvoiceBilling.Application.Common.Storage;

/// <summary>
/// Abstraction over PDF object storage (e.g. S3).
/// Defined in the Application layer to keep handlers free of infrastructure dependencies.
/// </summary>
public interface IInvoicePdfStorage
{
    /// <summary>
    /// Downloads the PDF for the given S3 key.
    /// Returns <c>null</c> when the object does not exist in the store.
    /// </summary>
    Task<InvoicePdfDownload?> TryDownloadAsync(string s3Key, string invoiceNumber, CancellationToken ct);
}

public sealed record InvoicePdfDownload(
    Stream ContentStream,
    string ContentType,
    string FileName
);
