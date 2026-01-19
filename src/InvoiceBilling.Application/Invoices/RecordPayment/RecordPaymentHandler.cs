using InvoiceBilling.Application.Common.Persistence;
using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Application.Invoices.RecordPayment;

public sealed class RecordPaymentHandler
    : IRequestHandler<RecordPaymentCommand, RecordPaymentResponse>
{
    private readonly IInvoiceBillingDbContext _db;

    public RecordPaymentHandler(IInvoiceBillingDbContext db)
    {
        _db = db;
    }

    public async Task<RecordPaymentResponse> Handle(RecordPaymentCommand request, CancellationToken cancellationToken)
    {
        if (request.InvoiceId == Guid.Empty)
        {
            return new RecordPaymentResponse(
                Succeeded: false,
                ErrorStatusCode: 400,
                ErrorTitle: "Validation failed",
                ErrorDetail: "InvoiceId is required.");
        }

        var amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0)
        {
            return new RecordPaymentResponse(
                Succeeded: false,
                ErrorStatusCode: 400,
                ErrorTitle: "Validation failed",
                ErrorDetail: "Amount must be > 0.");
        }

        var paidAtUtc = request.PaidAtUtc == default ? DateTime.UtcNow : request.PaidAtUtc;

        // Load with lines so the API can immediately return full invoice details.
        var invoice = await _db.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            return new RecordPaymentResponse(
                Succeeded: false,
                ErrorStatusCode: 404,
                ErrorTitle: "Invoice not found",
                ErrorDetail: $"Invoice {request.InvoiceId} was not found.");
        }

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            var payment = invoice.RecordPayment(
                amount: amount,
                paidAtUtc: paidAtUtc,
                method: request.Method,
                reference: request.Reference,
                note: request.Note);

            _db.Payments.Add(payment);

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new RecordPaymentResponse(
                Succeeded: true,
                Invoice: invoice,
                Payment: payment);
        }
        catch (DomainException ex)
        {
            var statusCode = MapDomainExceptionToStatus(ex.Message);

            return new RecordPaymentResponse(
                Succeeded: false,
                ErrorStatusCode: statusCode,
                ErrorTitle: statusCode == 409 ? "Invalid invoice state" : "Domain rule violation",
                ErrorDetail: ex.Message);
        }
    }

    private static int MapDomainExceptionToStatus(string message)
    {
        // Treat invalid state transitions and already-paid cases as 409; input/invariant violations as 400.
        if (message.Contains("Draft", StringComparison.OrdinalIgnoreCase)) return 409;
        if (message.Contains("Void", StringComparison.OrdinalIgnoreCase)) return 409;
        if (message.Contains("already", StringComparison.OrdinalIgnoreCase)) return 409;
        if (message.Contains("state", StringComparison.OrdinalIgnoreCase)) return 409;
        return 400;
    }
}
