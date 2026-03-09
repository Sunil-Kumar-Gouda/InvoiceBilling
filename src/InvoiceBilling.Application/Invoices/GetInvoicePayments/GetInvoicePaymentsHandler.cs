using InvoiceBilling.Application.Common.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Application.Invoices.GetInvoicePayments;

public sealed class GetInvoicePaymentsHandler : IRequestHandler<GetInvoicePaymentsQuery, GetInvoicePaymentsResponse>
{
    private readonly IInvoiceBillingDbContext _db;

    public GetInvoicePaymentsHandler(IInvoiceBillingDbContext db)
    {
        _db = db;
    }

    public async Task<GetInvoicePaymentsResponse> Handle(
        GetInvoicePaymentsQuery request,
        CancellationToken cancellationToken)
    {
        var invoiceExists = await _db.Invoices.AsNoTracking()
            .AnyAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (!invoiceExists)
            return new GetInvoicePaymentsResponse(
                Succeeded: false,
                ErrorStatusCode: 404,
                ErrorTitle: "Invoice not found",
                ErrorDetail: $"Invoice {request.InvoiceId} was not found.");

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.InvoiceId == request.InvoiceId)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new PaymentResult(
                p.Id,
                p.InvoiceId,
                p.Amount,
                p.PaidAt,
                p.Method,
                p.Reference,
                p.Note,
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        return new GetInvoicePaymentsResponse(Succeeded: true, Payments: payments);
    }
}
