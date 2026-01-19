namespace InvoiceBilling.Api.Dtos.Payments;

public sealed class RecordPaymentRequest
{
    public decimal Amount { get; set; }

    /// <summary>
    /// Payment timestamp in UTC.
    /// If omitted, the server may default to DateTime.UtcNow.
    /// </summary>
    public DateTime PaidAtUtc { get; set; }

    public string? Method { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
}
