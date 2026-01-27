using InvoiceBilling.Domain.Exceptions;

namespace InvoiceBilling.Domain.Entities;

/// <summary>
/// Payment recorded against an invoice.
///
/// Stored as a separate table (Payments). Invoice keeps derived aggregates
/// (PaidTotal/BalanceDue) for fast reads.
/// </summary>
public sealed class Payment
{
    private Payment() { } // EF Core

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }

    public decimal Amount { get; private set; }
    public DateTime PaidAt { get; private set; }

    public string? Method { get; private set; }
    public string? Reference { get; private set; }
    public string? Note { get; private set; }

    public DateTime CreatedAt { get; private set; }

    internal static Payment Create(
        Guid id,
        Guid invoiceId,
        decimal amount,
        DateTime paidAtUtc,
        string? method,
        string? reference,
        string? note,
        DateTime createdAtUtc)
    {
        if (invoiceId == Guid.Empty) throw new DomainException("InvoiceId is required.");
        if (amount <= 0) throw new DomainException("Payment Amount must be > 0.");
        if (paidAtUtc == default) throw new DomainException("PaidAtUtc is required.");

        var m = string.IsNullOrWhiteSpace(method) ? null : method.Trim();
        if (m is { Length: > 32 }) throw new DomainException("Payment Method is too long (max 32).");

        var r = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (r is { Length: > 64 }) throw new DomainException("Payment Reference is too long (max 64).");

        var n = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (n is { Length: > 512 }) throw new DomainException("Payment Note is too long (max 512).");

        return new Payment
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            InvoiceId = invoiceId,
            Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            PaidAt = DateTime.SpecifyKind(paidAtUtc, DateTimeKind.Utc),
            Method = m,
            Reference = r,
            Note = n,
            CreatedAt = createdAtUtc == default ? DateTime.UtcNow : DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        };
    }
}
