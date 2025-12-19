namespace InvoiceBilling.Api.Dtos.Customers;

public sealed class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? BusinessName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? BillingAddress { get; set; }
    public string? TaxId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
