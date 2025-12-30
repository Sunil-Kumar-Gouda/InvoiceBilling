using InvoiceBilling.Domain.Entities;

namespace InvoiceBilling.Domain.Services;

public interface IInvoiceTotalsCalculator
{
    InvoiceTotals Calculate(IReadOnlyCollection<InvoiceLine> lines, decimal taxRatePercent);

    /// <summary>
    /// Updates each line's LineTotal and sets invoice Subtotal/TaxTotal/GrandTotal based on TaxRatePercent.
    /// </summary>
    void Apply(Invoice invoice);
}
