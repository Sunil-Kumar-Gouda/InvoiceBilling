using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Domain.Exceptions;

namespace InvoiceBilling.Domain.Tests.Invoices;

public sealed class InvoicePaymentsTests
{
    [Fact]
    public void RecordPayment_throws_for_draft_invoice()
    {
        var invoice = CreateDraftInvoice();

        Action act = () => invoice.RecordPayment(
            amount: 10m,
            paidAtUtc: DateTime.UtcNow,
            method: "Cash",
            reference: null,
            note: null);

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot record payment for Draft invoices.");
    }

    [Fact]
    public void RecordPayment_reduces_balance_due_for_partial_payment_and_keeps_issued_status()
    {
        var invoice = CreateIssuedInvoice(grandTotal: 100m);

        var payment = invoice.RecordPayment(
            amount: 25.555m,
            paidAtUtc: new DateTime(2026, 01, 10, 10, 0, 0, DateTimeKind.Utc),
            method: "UPI",
            reference: "TXN-001",
            note: "Partial");

        invoice.Status.Should().Be(InvoiceStatus.Issued);
        invoice.PaidTotal.Should().Be(25.56m);
        invoice.BalanceDue.Should().Be(74.44m);

        payment.Amount.Should().Be(25.56m);
        payment.Method.Should().Be("UPI");
        payment.Reference.Should().Be("TXN-001");
    }

    [Fact]
    public void RecordPayment_transitions_to_paid_when_balance_is_zero()
    {
        var invoice = CreateIssuedInvoice(grandTotal: 100m);

        invoice.RecordPayment(
            amount: 100m,
            paidAtUtc: DateTime.UtcNow,
            method: "Bank",
            reference: "REF",
            note: null);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidTotal.Should().Be(100m);
        invoice.BalanceDue.Should().Be(0m);
    }

    [Fact]
    public void RecordPayment_throws_when_overpaying_balance_due()
    {
        var invoice = CreateIssuedInvoice(grandTotal: 100m);

        Action act = () => invoice.RecordPayment(
            amount: 100.01m,
            paidAtUtc: DateTime.UtcNow,
            method: null,
            reference: null,
            note: null);

        act.Should().Throw<DomainException>()
            .WithMessage("Payment cannot exceed BalanceDue.");
    }

    [Fact]
    public void AttachPdf_is_allowed_for_paid_invoice_to_support_async_worker_completion()
    {
        var invoice = CreateIssuedInvoice(grandTotal: 100m);

        invoice.RecordPayment(
            amount: 100m,
            paidAtUtc: DateTime.UtcNow,
            method: "Cash",
            reference: null,
            note: null);

        invoice.Status.Should().Be(InvoiceStatus.Paid);

        invoice.AttachPdf("invoices/paid-invoice.pdf");

        invoice.PdfS3Key.Should().Be("invoices/paid-invoice.pdf");
    }

    private static Invoice CreateDraftInvoice(decimal grandTotal = 100m)
    {
        return Invoice.CreateDraft(
            id: Guid.NewGuid(),
            invoiceNumber: $"INV-PAY-{Guid.NewGuid():N}".Substring(0, 20),
            customerId: Guid.NewGuid(),
            issueDate: new DateTime(2026, 01, 01),
            dueDate: new DateTime(2026, 01, 10),
            currencyCode: "INR",
            createdAtUtc: DateTime.UtcNow,
            lines: new[] { (Guid.NewGuid(), "L1", grandTotal, 1m) });
    }

    private static Invoice CreateIssuedInvoice(decimal grandTotal = 100m)
    {
        var invoice = CreateDraftInvoice(grandTotal);
        invoice.Issue(new DateTime(2026, 01, 01, 10, 0, 0, DateTimeKind.Utc));
        invoice.Status.Should().Be(InvoiceStatus.Issued);
        return invoice;
    }
}
