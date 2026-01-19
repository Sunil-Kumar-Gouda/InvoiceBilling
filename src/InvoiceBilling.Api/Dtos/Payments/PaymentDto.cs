namespace InvoiceBilling.Api.Dtos.Payments;

public sealed class PaymentDto
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }

    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }

    public string? Method { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
}
