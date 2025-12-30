namespace InvoiceBilling.Domain.Services;

public sealed record InvoiceTotals(
    decimal Subtotal,
    decimal TaxTotal,
    decimal GrandTotal
);
