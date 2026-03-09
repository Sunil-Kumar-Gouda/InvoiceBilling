using InvoiceBilling.Application.Common.Persistence;
using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Application.Invoices.CreateInvoice;

public sealed class CreateInvoiceHandler : IRequestHandler<CreateInvoiceCommand, CreateInvoiceResponse>
{
    private readonly IInvoiceBillingDbContext _db;

    public CreateInvoiceHandler(IInvoiceBillingDbContext db)
    {
        _db = db;
    }

    public async Task<CreateInvoiceResponse> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
            return Fail(400, "Validation failed", "CustomerId is required.");

        if (request.Lines is null || request.Lines.Count == 0)
            return Fail(400, "Validation failed", "At least one line is required.");

        var customerExists = await _db.Customers.AsNoTracking()
            .AnyAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (!customerExists)
            return Fail(400, "Validation failed", $"Unknown CustomerId: {request.CustomerId}");

        var productIds = request.Lines
            .Select(l => l.ProductId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        var existingProductIds = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var missingProductIds = productIds.Except(existingProductIds).ToArray();
        if (missingProductIds.Length > 0)
            return Fail(400, "Validation failed", $"Unknown ProductId(s): {string.Join(", ", missingProductIds)}");

        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Random.Shared.Next(1000, 9999)}";
        var issueDate = (request.IssueDate is null || request.IssueDate == default)
            ? DateTime.UtcNow.Date
            : request.IssueDate.Value.Date;
        var dueDate = (request.DueDate is null || request.DueDate == default)
            ? issueDate.AddDays(7)
            : request.DueDate.Value.Date;

        var lines = request.Lines.Select(l => (l.ProductId, l.Description, l.UnitPrice, l.Quantity));

        try
        {
            var invoice = Invoice.CreateDraft(
                id: Guid.NewGuid(),
                invoiceNumber: invoiceNumber,
                customerId: request.CustomerId,
                issueDate: issueDate,
                dueDate: dueDate,
                currencyCode: request.CurrencyCode,
                createdAtUtc: DateTime.UtcNow,
                lines: lines);

            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync(cancellationToken);

            return new CreateInvoiceResponse(Succeeded: true, Invoice: invoice);
        }
        catch (DomainException ex)
        {
            return Fail(MapDomainExceptionToStatus(ex.Message), "Domain rule violation", ex.Message);
        }
    }

    private static CreateInvoiceResponse Fail(int statusCode, string title, string detail) =>
        new(Succeeded: false, ErrorStatusCode: statusCode, ErrorTitle: title, ErrorDetail: detail);

    private static int MapDomainExceptionToStatus(string message)
    {
        if (message.Contains("Only Draft", StringComparison.OrdinalIgnoreCase)) return 409;
        if (message.Contains("already", StringComparison.OrdinalIgnoreCase)) return 409;
        return 400;
    }
}
