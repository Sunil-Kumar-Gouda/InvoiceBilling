using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Domain.Exceptions;

namespace InvoiceBilling.Domain.Tests.Invoices;

public sealed class InvoiceIssueAndPdfTests
{
    [Fact]
    public void Issue_transitions_to_Issued_and_sets_issueDate()
    {
        var invoice = Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: "INV-ISSUE-001",
            customerId: Guid.NewGuid(),
            issueDate: new DateTime(2026, 01, 01),
            dueDate: new DateTime(2026, 01, 10),
            currencyCode: "INR",
            createdAtUtc: DateTime.UtcNow,
            lines: new[] { (Guid.NewGuid(), "L1", 100m, 1m) });

        invoice.Issue(new DateTime(2026, 01, 01, 10, 30, 00, DateTimeKind.Utc));

        invoice.Status.Should().Be("Issued");
        invoice.IssueDate.Date.Should().Be(new DateTime(2026, 01, 01));
    }

    [Fact]
    public void Issue_throws_if_already_not_draft()
    {
        var invoice = Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: "INV-ISSUE-002",
            customerId: Guid.NewGuid(),
            issueDate: new DateTime(2026, 01, 01),
            dueDate: new DateTime(2026, 01, 10),
            currencyCode: "INR",
            createdAtUtc: DateTime.UtcNow,
            lines: new[] { (Guid.NewGuid(), "L1", 100m, 1m) });

        invoice.Issue(new DateTime(2026, 01, 01));

        Action act = () => invoice.ReplaceLines(new[] { (Guid.NewGuid(), "L2", 10m, 1m) });

        act.Should().Throw<DomainException>()
            .WithMessage("Only Draft invoices can be modified.");
    }

    [Fact]
    public void Issue_throws_when_dueDate_before_new_issueDate()
    {
        var invoice = Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: "INV-ISSUE-003",
            customerId: Guid.NewGuid(),
            issueDate: new DateTime(2026, 01, 01),
            dueDate: new DateTime(2026, 01, 10),
            currencyCode: "INR",
            createdAtUtc: DateTime.UtcNow,
            lines: new[] { (Guid.NewGuid(), "L1", 100m, 1m) });

        Action act = () => invoice.Issue(new DateTime(2026, 02, 01));

        act.Should().Throw<DomainException>()
            .WithMessage("DueDate cannot be before IssueDate.");
    }

    [Fact]
    public void AttachPdf_sets_key_and_requires_non_empty()
    {
        var invoice = Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: "INV-PDF-001",
            customerId: Guid.NewGuid(),
            issueDate: new DateTime(2026, 01, 01),
            dueDate: new DateTime(2026, 01, 10),
            currencyCode: "INR",
            createdAtUtc: DateTime.UtcNow,
            lines: new[] { (Guid.NewGuid(), "L1", 100m, 1m) });

        Action bad = () => invoice.AttachPdf("  ");
        bad.Should().Throw<DomainException>().WithMessage("PdfS3Key is required.");
        invoice.Issue(new DateTime(2026, 01, 01));
        invoice.AttachPdf("invoices/abc.pdf");
        invoice.PdfS3Key.Should().Be("invoices/abc.pdf");
    }
}
