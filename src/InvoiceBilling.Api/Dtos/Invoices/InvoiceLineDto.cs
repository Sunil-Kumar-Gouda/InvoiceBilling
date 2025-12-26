namespace InvoiceBilling.Api.Dtos.Invoices;

public sealed class InvoiceLineDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Description { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
