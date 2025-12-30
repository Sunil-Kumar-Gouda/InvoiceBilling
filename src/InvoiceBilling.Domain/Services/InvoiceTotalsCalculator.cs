using InvoiceBilling.Domain.Entities;

namespace InvoiceBilling.Domain.Services;

public sealed class InvoiceTotalsCalculator : IInvoiceTotalsCalculator
{
    public InvoiceTotals Calculate(IReadOnlyCollection<InvoiceLine> lines, decimal taxRatePercent)
    {
        if (lines is null || lines.Count == 0)
            return new InvoiceTotals(0m, 0m, 0m);

        var subtotal = 0m;

        foreach (var l in lines)
        {
            // Always compute line totals consistently
            var lineTotal = l.UnitPrice * l.Quantity;
            subtotal += lineTotal;
        }

        var taxRate = NormalizeTaxRate(taxRatePercent);
        var taxTotal = RoundMoney(subtotal * (taxRate / 100m));
        var grandTotal = RoundMoney(subtotal + taxTotal);

        // Subtotal itself can also be rounded depending on your policy
        subtotal = RoundMoney(subtotal);

        return new InvoiceTotals(subtotal, taxTotal, grandTotal);
    }

    public void Apply(Invoice invoice)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));

        // Update line totals first
        foreach (var l in invoice.Lines)
        {
            l.LineTotal = RoundMoney(l.UnitPrice * l.Quantity);
        }

        var totals = Calculate(invoice.Lines, invoice.TaxRatePercent);
        invoice.Subtotal = totals.Subtotal;
        invoice.TaxTotal = totals.TaxTotal;
        invoice.GrandTotal = totals.GrandTotal;
    }

    private static decimal NormalizeTaxRate(decimal taxRatePercent)
    {
        // Guardrails: keep tax rate within a sane range
        if (taxRatePercent < 0m) return 0m;
        if (taxRatePercent > 100m) return 100m;
        return taxRatePercent;
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
