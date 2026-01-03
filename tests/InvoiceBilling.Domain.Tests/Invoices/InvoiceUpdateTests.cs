using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Domain.Exceptions;

namespace InvoiceBilling.Domain.Tests.Invoices;

public sealed class InvoiceUpdateTests
{
    [Fact]
    public void UpdateDraftHeader_updates_dueDate_currency_tax_and_recalculates()
    {
        var invoice = CreateSampleDraft(subtotal: 10.05m);

        // Set tax to 10% and ensure rounding policy: 10.05 * 10% = 1.005 -> 1.01 (AwayFromZero)
        invoice.UpdateDraftHeader(
            dueDate: new DateTime(2026, 01, 20),
            currencyCode: "usd",
            taxRatePercent: 10m);

        invoice.CurrencyCode.Should().Be("USD");
        invoice.DueDate.Date.Should().Be(new DateTime(2026, 01, 20));

        invoice.Subtotal.Should().Be(10.05m);
        invoice.TaxTotal.Should().Be(1.01m);
        invoice.GrandTotal.Should().Be(11.06m);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void UpdateDraftHeader_throws_when_taxRate_out_of_range(decimal taxRate)
    {
        var invoice = CreateSampleDraft(subtotal: 100m);

        Action act = () => invoice.UpdateDraftHeader(
            dueDate: invoice.DueDate,
            currencyCode: invoice.CurrencyCode,
            taxRatePercent: taxRate);

        act.Should().Throw<DomainException>()
            .WithMessage("TaxRatePercent must be between 0 and 100.");
    }

    [Fact]
    public void ReplaceLines_replaces_lines_and_recalculates()
    {
        var invoice = CreateSampleDraft(subtotal: 100m);

        invoice.ReplaceLines(new[]
        {
            (productId: Guid.NewGuid(), description: "New 1", unitPrice: 25m, quantity: 2m),  // 50
            (productId: Guid.NewGuid(), description: "New 2", unitPrice: 10m, quantity: 3m),  // 30
        });

        invoice.Subtotal.Should().Be(80m);
        invoice.Lines.Should().HaveCount(2);
        invoice.Lines.Sum(l => l.LineTotal).Should().Be(80m);
    }

    [Fact]
    public void ReplaceLines_throws_when_empty()
    {
        var invoice = CreateSampleDraft(subtotal: 100m);

        Action act = () => invoice.ReplaceLines(
            Array.Empty<(Guid productId, string description, decimal unitPrice, decimal quantity)>());

        act.Should().Throw<DomainException>()
            .WithMessage("At least one line is required.");
    }

    private static Invoice CreateSampleDraft(decimal subtotal)
    {
        // Build a single line that yields the desired subtotal precisely
        var invoice = Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: "INV-UT-001",
            customerId: Guid.NewGuid(),
            issueDate: new DateTime(2026, 01, 01),
            dueDate: new DateTime(2026, 01, 10),
            currencyCode: "INR",
            createdAtUtc: DateTime.UtcNow,
            lines: new[] { (Guid.NewGuid(), "L1", unitPrice: subtotal, quantity: 1m) });

        invoice.Subtotal.Should().Be(subtotal);
        return invoice;
    }
}
