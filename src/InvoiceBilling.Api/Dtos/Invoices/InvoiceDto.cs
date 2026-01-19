namespace InvoiceBilling.Api.Dtos.Invoices;

public sealed class InvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = default!;
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }

    public string CurrencyCode { get; set; } = default!;
    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    // Day 13: Payments
    public decimal PaidTotal { get; set; }
    public decimal BalanceDue { get; set; }

    public string? PdfS3Key { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<InvoiceLineDto> Lines { get; set; } = new();
    public decimal TaxRatePercent { get; set; }
}
