using InvoiceBilling.Domain.Exceptions;

namespace InvoiceBilling.Domain.Entities;

public sealed class Invoice
{
    private readonly List<InvoiceLine> _lines = new();

    private Invoice() { } // EF Core

    public Guid Id { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    public string Status { get; private set; } = InvoiceStatus.Draft;

    public DateTime IssueDate { get; private set; }
    public DateTime DueDate { get; private set; }

    public string CurrencyCode { get; private set; } = "INR";

    public decimal TaxRatePercent { get; private set; } = 0m;

    public decimal Subtotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }

    public decimal PaidTotal { get; private set; }
    public decimal BalanceDue { get; private set; }

    public string? PdfS3Key { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<InvoiceLine> Lines => _lines;

    public static Invoice CreateDraft(
        Guid id,
        string invoiceNumber,
        Guid customerId,
        DateTime issueDate,
        DateTime dueDate,
        string currencyCode,
        DateTime createdAtUtc,
        IEnumerable<(Guid productId, string description, decimal unitPrice, decimal quantity)> lines)
    {
        if (id == Guid.Empty) id = Guid.NewGuid();

        var invNo = (invoiceNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(invNo)) throw new DomainException("InvoiceNumber is required.");

        if (customerId == Guid.Empty) throw new DomainException("CustomerId is required.");

        var cc = NormalizeCurrency(currencyCode);
        var iDate = issueDate == default ? DateTime.UtcNow.Date : issueDate.Date;
        var dDate = dueDate == default ? iDate.AddDays(7) : dueDate.Date;

        if (dDate < iDate) throw new DomainException("DueDate cannot be before IssueDate.");

        var invoice = new Invoice
        {
            Id = id,
            InvoiceNumber = invNo,
            CustomerId = customerId,
            Status = InvoiceStatus.Draft,
            IssueDate = iDate,
            DueDate = dDate,
            CurrencyCode = cc,
            CreatedAt = createdAtUtc == default ? DateTime.UtcNow : createdAtUtc
        };

        invoice.ReplaceLines(lines);
        invoice.SetTaxRate(0m); // default (you can pass in later)
        invoice.RecalculateTotals();
        return invoice;
    }

    public void UpdateDraftHeader(DateTime dueDate, string currencyCode, decimal taxRatePercent)
    {
        EnsureDraft();

        var d = dueDate == default ? DueDate : dueDate.Date;
        if (d < IssueDate.Date) throw new DomainException("DueDate cannot be before IssueDate.");

        CurrencyCode = NormalizeCurrency(currencyCode ?? CurrencyCode);
        SetTaxRate(taxRatePercent);

        DueDate = d;

        RecalculateTotals();
    }

    public void ReplaceLines(IEnumerable<(Guid productId, string description, decimal unitPrice, decimal quantity)> lines)
    {
        EnsureDraft();

        if (lines is null) throw new DomainException("At least one line is required.");

        var materialized = lines.ToList();
        if (materialized.Count == 0) throw new DomainException("At least one line is required.");

        _lines.Clear();

        foreach (var l in materialized)
        {
            var line = InvoiceLine.Create(
                id: Guid.NewGuid(),
                invoiceId: Id,
                productId: l.productId,
                description: l.description,
                unitPrice: l.unitPrice,
                quantity: l.quantity);

            _lines.Add(line);
        }

        RecalculateTotals();
    }

    public void Issue(DateTime issuedAtUtc)
    {
        EnsureDraft();

        if (_lines.Count == 0) throw new DomainException("Cannot issue an invoice without lines.");

        Status = InvoiceStatus.Issued;
        IssueDate = (issuedAtUtc == default ? DateTime.UtcNow.Date : issuedAtUtc.Date);

        if (DueDate.Date < IssueDate.Date)
            throw new DomainException("DueDate cannot be before IssueDate.");

        RecalculateTotals();
    }

    /// <summary>
    /// Records a payment against this invoice.
    /// Business rules:
    /// - Only Issued invoices can be paid (Draft/Void/Paid are rejected).
    /// - Payment amount must be > 0.
    /// - Payment cannot exceed BalanceDue.
    /// - When BalanceDue becomes 0, invoice transitions to Paid.
    /// </summary>
    public Payment RecordPayment(decimal amount, DateTime paidAtUtc, string? method, string? reference, string? note)
    {
        var status = Status ?? string.Empty;

        if (string.Equals(status, InvoiceStatus.Draft, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Cannot record payment for Draft invoices.");

        if (string.Equals(status, InvoiceStatus.Void, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Cannot record payment for Void invoices.");

        if (string.Equals(status, InvoiceStatus.Paid, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Invoice is already Paid.");

        if (!string.Equals(status, InvoiceStatus.Issued, StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"Cannot record payment when invoice is in '{status}' state.");

        var amt = RoundMoney(amount);
        if (amt <= 0) throw new DomainException("Payment amount must be > 0.");

        if (BalanceDue <= 0)
            throw new DomainException("Invoice has no balance due.");

        if (amt > BalanceDue)
            throw new DomainException("Payment cannot exceed BalanceDue.");

        PaidTotal = RoundMoney(PaidTotal + amt);
        BalanceDue = RoundMoney(GrandTotal - PaidTotal);

        // Guard against -0.00 due to rounding.
        if (BalanceDue < 0) BalanceDue = 0m;

        if (BalanceDue == 0m)
            Status = InvoiceStatus.Paid;

        return Payment.Create(
            id: Guid.NewGuid(),
            invoiceId: Id,
            amount: amt,
            paidAtUtc: paidAtUtc,
            method: method,
            reference: reference,
            note: note,
            createdAtUtc: DateTime.UtcNow);
    }

    public void AttachPdf(string s3Key)
    {
        var key = (s3Key ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("PdfS3Key is required.");

        // Business rule: PDF should only be attached after issuing (or after it becomes Paid).
        var attachable = string.Equals(Status, InvoiceStatus.Issued, StringComparison.OrdinalIgnoreCase)
                      || string.Equals(Status, InvoiceStatus.Paid, StringComparison.OrdinalIgnoreCase);

        if (!attachable)
            throw new DomainException("PDF can be attached only for Issued or Paid invoices.");

        // Business rule: do not overwrite an existing attachment
        if (!string.IsNullOrWhiteSpace(PdfS3Key))
            throw new DomainException("PDF is already attached for this invoice.");

        PdfS3Key = key;
    }

    private void EnsureDraft()
    {
        if (!string.Equals(Status, InvoiceStatus.Draft, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Only Draft invoices can be modified.");
    }

    private void SetTaxRate(decimal taxRatePercent)
    {
        if (taxRatePercent < 0 || taxRatePercent > 100)
            throw new DomainException("TaxRatePercent must be between 0 and 100.");

        TaxRatePercent = taxRatePercent;
    }

    internal void RecalculateTotals()
    {
        foreach (var line in _lines)
            line.Recalculate();

        var rawSubtotal = _lines.Sum(x => x.LineTotal);

        Subtotal = RoundMoney(rawSubtotal);
        TaxTotal = RoundMoney(Subtotal * (TaxRatePercent / 100m));
        GrandTotal = RoundMoney(Subtotal + TaxTotal);

        // Keep payments-derived aggregates consistent.
        BalanceDue = RoundMoney(GrandTotal - PaidTotal);
        if (BalanceDue < 0) BalanceDue = 0m;
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);


    private static string NormalizeCurrency(string? currencyCode)
    {
        var cc = (currencyCode ?? "INR").Trim().ToUpperInvariant();
        if (cc.Length != 3) throw new DomainException("CurrencyCode must be a 3-letter ISO code.");
        return cc;
    }

    internal void ApplyTotals(Services.InvoiceTotals totals)
    {
        Subtotal = totals.Subtotal;
        TaxTotal = totals.TaxTotal;
        GrandTotal = totals.GrandTotal;

        BalanceDue = RoundMoney(GrandTotal - PaidTotal);
        if (BalanceDue < 0) BalanceDue = 0m;
    }
}
