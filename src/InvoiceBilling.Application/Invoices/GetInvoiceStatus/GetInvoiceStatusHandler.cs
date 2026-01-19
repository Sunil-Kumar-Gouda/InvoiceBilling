using InvoiceBilling.Application.Common.Persistence;
using InvoiceBilling.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Application.Invoices.GetInvoiceStatus;

public sealed class GetInvoiceStatusHandler : IRequestHandler<GetInvoiceStatusQuery, GetInvoiceStatusResponse>
{
    private readonly IInvoiceBillingDbContext _db;

    public GetInvoiceStatusHandler(IInvoiceBillingDbContext db)
    {
        _db = db;
    }

    public async Task<GetInvoiceStatusResponse> Handle(GetInvoiceStatusQuery request, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        var state = await _db.Invoices.AsNoTracking()
            .Where(i => i.Id == request.InvoiceId)
            .Select(i => new
            {
                i.Id,
                i.Status,
                i.DueDate,
                i.PaidTotal,
                i.BalanceDue,
                i.PdfS3Key
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (state is null)
        {
            return new GetInvoiceStatusResponse(
                Succeeded: false,
                ErrorStatusCode: 404,
                ErrorTitle: "Invoice not found",
                ErrorDetail: $"Invoice {request.InvoiceId} was not found.");
        }

        var effectiveStatus = (state.Status == InvoiceStatus.Issued && state.DueDate < today && state.BalanceDue > 0)
            ? InvoiceStatus.Overdue
            : state.Status;

        var model = new InvoiceStatusState(
            state.Id,
            state.Status,
            effectiveStatus,
            state.DueDate,
            state.PaidTotal,
            state.BalanceDue,
            state.PdfS3Key);

        return new GetInvoiceStatusResponse(
            Succeeded: true,
            State: model);
    }
}
