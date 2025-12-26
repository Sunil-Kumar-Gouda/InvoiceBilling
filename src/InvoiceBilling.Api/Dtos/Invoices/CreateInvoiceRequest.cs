namespace InvoiceBilling.Api.Dtos.Invoices;

public sealed class CreateInvoiceRequest
{
    public Guid CustomerId { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public string CurrencyCode { get; set; } = "INR";
    public List<CreateInvoiceLineRequest> Lines { get; set; } = new();
}

public sealed class CreateInvoiceLineRequest
{
    public Guid ProductId { get; set; }
    public string Description { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
}
