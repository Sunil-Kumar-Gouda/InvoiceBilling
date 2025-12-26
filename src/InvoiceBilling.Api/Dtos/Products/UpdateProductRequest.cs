namespace InvoiceBilling.Api.Dtos.Products;

public sealed class UpdateProductRequest
{
    public string Name { get; set; } = default!;
    public string? Sku { get; set; }
    public decimal UnitPrice { get; set; }
    public string CurrencyCode { get; set; } = "INR";
}
