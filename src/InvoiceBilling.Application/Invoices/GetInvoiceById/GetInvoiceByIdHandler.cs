using InvoiceBilling.Application.Common.Persistence;
using InvoiceBilling.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Application.Invoices.GetInvoiceById;

public sealed class GetInvoiceByIdHandler : IRequestHandler<GetInvoiceByIdQuery, GetInvoiceByIdResponse>
{
    private readonly IInvoiceBillingDbContext _db;

    public GetInvoiceByIdHandler(IInvoiceBillingDbContext db)
    {
        _db = db;
    }

    public async Task<GetInvoiceByIdResponse> Handle(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            return new GetInvoiceByIdResponse(
                Succeeded: false,
                ErrorStatusCode: 404,
                ErrorTitle: "Invoice not found",
                ErrorDetail: $"Invoice {request.InvoiceId} was not found.");
        }

        var today = DateTime.UtcNow.Date;
        var effectiveStatus = (invoice.Status == InvoiceStatus.Issued && invoice.DueDate < today && invoice.BalanceDue > 0)
            ? InvoiceStatus.Overdue
            : invoice.Status;

        var lines = invoice.Lines
            .Select(l => new InvoiceLineDetails(
                l.Id,
                l.ProductId,
                l.Description,
                l.UnitPrice,
                l.Quantity,
                l.LineTotal))
            .ToList();

        var details = new InvoiceDetails(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.CustomerId,
            effectiveStatus,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.CurrencyCode,
            invoice.TaxRatePercent,
            invoice.Subtotal,
            invoice.TaxTotal,
            invoice.GrandTotal,
            invoice.PaidTotal,
            invoice.BalanceDue,
            invoice.PdfS3Key,
            invoice.CreatedAt,
            lines);

        return new GetInvoiceByIdResponse(
            Succeeded: true,
            Invoice: details);
    }
}
