using InvoiceBilling.Domain.Exceptions;

namespace InvoiceBilling.Domain.Entities;

public sealed class InvoiceLine
{
    private InvoiceLine() { } // EF Core

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }

    public Guid ProductId { get; private set; }
    public string Description { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }
    public decimal Quantity { get; private set; }

    public decimal LineTotal { get; private set; }

    internal static InvoiceLine Create(
        Guid id,
        Guid invoiceId,
        Guid productId,
        string description,
        decimal unitPrice,
        decimal quantity)
    {
        if (invoiceId == Guid.Empty) throw new DomainException("InvoiceId is required.");
        if (productId == Guid.Empty) throw new DomainException("ProductId is required.");

        var desc = (description ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(desc)) throw new DomainException("Line Description is required.");

        if (quantity <= 0) throw new DomainException("Line Quantity must be > 0.");
        if (unitPrice < 0) throw new DomainException("Line UnitPrice must be >= 0.");

        var line = new InvoiceLine
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            InvoiceId = invoiceId,
            ProductId = productId,
            Description = desc,
            UnitPrice = unitPrice,
            Quantity = quantity
        };

        line.Recalculate();
        return line;
    }

    internal void Recalculate()
    {
        // Centralize money rounding policy here
        LineTotal = RoundMoney(UnitPrice * Quantity);
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    internal void SetLineTotal(decimal lineTotal)
    {
        LineTotal = lineTotal;
    }
}
