namespace InvoiceBilling.Api.Dtos.Customers;

public sealed class UpdateCustomerRequest
{
    public string Name { get; set; } = default!;
    public string? BusinessName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? BillingAddress { get; set; }
    public string? TaxId { get; set; }
}
