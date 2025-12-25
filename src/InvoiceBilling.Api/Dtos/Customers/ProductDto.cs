namespace InvoiceBilling.Api.Dtos.Products;

public sealed class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Sku { get; set; }
    public decimal UnitPrice { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
