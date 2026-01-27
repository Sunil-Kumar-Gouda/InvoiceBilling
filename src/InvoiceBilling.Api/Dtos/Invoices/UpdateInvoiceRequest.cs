namespace InvoiceBilling.Api.Dtos.Invoices;

public sealed class UpdateInvoiceRequest
{
    public DateTime DueDate { get; set; }
    public string CurrencyCode { get; set; } = "INR";

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
