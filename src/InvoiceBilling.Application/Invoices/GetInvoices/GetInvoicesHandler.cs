using InvoiceBilling.Application.Common.Persistence;
using InvoiceBilling.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Application.Invoices.GetInvoices;

public sealed class GetInvoicesHandler : IRequestHandler<GetInvoicesQuery, GetInvoicesResponse>
{
    private readonly IInvoiceBillingDbContext _db;

    public GetInvoicesHandler(IInvoiceBillingDbContext db)
    {
        _db = db;
    }

    public async Task<GetInvoicesResponse> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 1 : request.PageSize > 200 ? 200 : request.PageSize;

        var today = DateTime.UtcNow.Date;
        var q = _db.Invoices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var s = request.Status.Trim();

            if (string.Equals(s, InvoiceStatus.Overdue, StringComparison.OrdinalIgnoreCase))
            {
                // Overdue is derived: Issued + DueDate < today + BalanceDue > 0
                q = q.Where(i => i.Status == InvoiceStatus.Issued && i.DueDate < today && i.BalanceDue > 0);
            }
            else if (string.Equals(s, InvoiceStatus.Issued, StringComparison.OrdinalIgnoreCase))
            {
                // Exclude derived Overdue from the Issued filter.
                q = q.Where(i => i.Status == InvoiceStatus.Issued && i.DueDate >= today);
            }
            else
            {
                q = q.Where(i => i.Status == s);
            }
        }

        if (request.CustomerId.HasValue && request.CustomerId.Value != Guid.Empty)
            q = q.Where(i => i.CustomerId == request.CustomerId.Value);

        if (request.IssueDateFrom.HasValue)
            q = q.Where(i => i.IssueDate >= request.IssueDateFrom.Value.Date);

        if (request.IssueDateTo.HasValue)
            q = q.Where(i => i.IssueDate <= request.IssueDateTo.Value.Date);

        var items = await q
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceListItem(
                i.Id,
                i.InvoiceNumber,
                i.CustomerId,
                i.Status == InvoiceStatus.Issued && i.DueDate < today && i.BalanceDue > 0
                    ? InvoiceStatus.Overdue
                    : i.Status,
                i.IssueDate,
                i.DueDate,
                i.CurrencyCode,
                i.TaxRatePercent,
                i.Subtotal,
                i.TaxTotal,
                i.GrandTotal,
                i.PaidTotal,
                i.BalanceDue,
                i.PdfS3Key,
                i.CreatedAt))
            .ToListAsync(cancellationToken);

        return new GetInvoicesResponse(
            Succeeded: true,
            Items: items,
            Page: page,
            PageSize: pageSize);
    }
}
