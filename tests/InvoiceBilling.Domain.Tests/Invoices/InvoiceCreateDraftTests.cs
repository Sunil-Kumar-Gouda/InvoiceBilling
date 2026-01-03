using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Domain.Exceptions;

namespace InvoiceBilling.Domain.Tests.Invoices;

public sealed class InvoiceCreateDraftTests
{
    [Fact]
    public void CreateDraft_sets_Draft_status_and_calculates_totals()
    {
        var invoice = Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: "INV-001",
            customerId: Guid.NewGuid(),
            issueDate: new DateTime(2026, 01, 01),
            dueDate: new DateTime(2026, 01, 08),
            currencyCode: "inr",
            createdAtUtc: new DateTime(),
            lines: new[]
            {
                (productId: Guid.NewGuid(), description: "Line 1", unitPrice: 100m, quantity: 2m),
                (productId: Guid.NewGuid(), description: "Line 2", unitPrice: 50m,  quantity: 1m)
            });

        invoice.Status.Should().Be("Draft");
        invoice.CurrencyCode.Should().Be("INR");

        // Line totals are unitPrice * quantity; tax defaults to 0 in CreateDraft
        invoice.Subtotal.Should().Be(250m);
        invoice.TaxTotal.Should().Be(0m);
        invoice.GrandTotal.Should().Be(250m);

        invoice.Lines.Should().HaveCount(2);
        invoice.Lines.Sum(x => x.LineTotal).Should().Be(250m);
    }

    [Fact]
    public void CreateDraft_throws_when_invoiceNumber_missing()
    {
        Action act = () => Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: "  ",
            customerId: Guid.NewGuid(),
            issueDate: new DateTime(2026, 01, 01),
            dueDate: new DateTime(2026, 01, 08),
            currencyCode: "INR",
            createdAtUtc: DateTime.UtcNow,
            lines: new[] { (Guid.NewGuid(), "Line", 10m, 1m) });

        act.Should().Throw<DomainException>()
            .WithMessage("InvoiceNumber is required.");
    }

    [Fact]
    public void CreateDraft_throws_when_customerId_empty()
    {
        Action act = () => Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: "INV-001",
            customerId: Guid.Empty,
            issueDate: new DateTime(2026, 01, 01),
            dueDate: new DateTime(2026, 01, 08),
            currencyCode: "INR",
            createdAtUtc: DateTime.UtcNow,
            lines: new[] { (Guid.NewGuid(), "Line", 10m, 1m) });

        act.Should().Throw<DomainException>()
            .WithMessage("CustomerId is required.");
    }

    [Fact]
    public void CreateDraft_throws_when_dueDate_before_issueDate()
    {
        Action act = () => Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: "INV-001",
            customerId: Guid.NewGuid(),
            issueDate: new DateTime(2026, 01, 10),
            dueDate: new DateTime(2026, 01, 01),
            currencyCode: "INR",
            createdAtUtc: DateTime.UtcNow,
            lines: new[] { (Guid.NewGuid(), "Line", 10m, 1m) });

        act.Should().Throw<DomainException>()
            .WithMessage("DueDate cannot be before IssueDate.");
    }

    [Fact]
    public void CreateDraft_throws_when_lines_empty()
    {
        Action act = () => Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: "INV-001",
            customerId: Guid.NewGuid(),
            issueDate: new DateTime(2026, 01, 01),
            dueDate: new DateTime(2026, 01, 08),
            currencyCode: "INR",
            createdAtUtc: DateTime.UtcNow,
            lines: Array.Empty<(Guid productId, string description, decimal unitPrice, decimal quantity)>());

        act.Should().Throw<DomainException>()
            .WithMessage("At least one line is required.");
    }

    [Fact]
    public void CreateDraft_throws_when_currency_invalid()
    {
        Action act = () => Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: "INV-001",
            customerId: Guid.NewGuid(),
            issueDate: new DateTime(2026, 01, 01),
            dueDate: new DateTime(2026, 01, 08),
            currencyCode: "RUPEE",
            createdAtUtc: DateTime.UtcNow,
            lines: new[] { (Guid.NewGuid(), "Line", 10m, 1m) });

        act.Should().Throw<DomainException>()
            .WithMessage("CurrencyCode must be a 3-letter ISO code.");
    }
}
