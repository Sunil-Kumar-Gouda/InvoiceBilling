namespace InvoiceBilling.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Sku { get; set; }
    public decimal UnitPrice { get; set; }
    public string CurrencyCode { get; set; } = "INR";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
