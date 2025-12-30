namespace InvoiceBilling.Api.Dtos.Invoices;

public sealed class UpdateInvoiceRequest
{
    public DateTime DueDate { get; set; }
    public string CurrencyCode { get; set; } = "INR";

    // Day 6: allow changing tax rate for the whole invoice
    public decimal TaxRatePercent { get; set; } = 0m;

    public List<UpdateInvoiceLineRequest> Lines { get; set; } = new();
}

public sealed class UpdateInvoiceLineRequest
{
    public Guid ProductId { get; set; }
    public string Description { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
}
