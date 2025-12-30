namespace InvoiceBilling.Domain.Entities;

public sealed class Invoice
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public Guid CustomerId { get; set; }

    public string Status { get; set; } = "Draft"; // Draft | Issued
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TaxRatePercent { get; set; } = 0m; // e.g., 0, 5, 18

    public string CurrencyCode { get; set; } = "INR";
    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public string? PdfS3Key { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<InvoiceLine> Lines { get; set; } = new();
}
