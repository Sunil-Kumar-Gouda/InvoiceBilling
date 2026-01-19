namespace InvoiceBilling.Api.Dtos.Invoices;

/// <summary>
/// Lightweight status contract for UI polling.
/// </summary>
public sealed class InvoiceStatusDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = default!;

    // Day 13: Payments
    public decimal PaidTotal { get; set; }
    public decimal BalanceDue { get; set; }

    /// <summary>
    /// One of: NotIssued, Pending, Ready.
    /// </summary>
    public string PdfStatus { get; set; } = default!;

    /// <summary>
    /// Relative URL to download the PDF when PdfStatus == Ready.
    /// </summary>
    public string? PdfDownloadUrl { get; set; }
}
