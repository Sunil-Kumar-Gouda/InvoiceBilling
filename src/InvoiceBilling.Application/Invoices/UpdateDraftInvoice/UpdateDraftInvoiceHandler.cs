using InvoiceBilling.Application.Common.Persistence;
using InvoiceBilling.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;


namespace InvoiceBilling.Application.Invoices.UpdateDraftInvoice;

public sealed class UpdateDraftInvoiceHandler
    : IRequestHandler<UpdateDraftInvoiceCommand, UpdateDraftInvoiceResponse>
{
    private readonly IInvoiceBillingDbContext _db;

    public UpdateDraftInvoiceHandler(IInvoiceBillingDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateDraftInvoiceResponse> Handle(UpdateDraftInvoiceCommand request, CancellationToken cancellationToken)
    {
        if (request.InvoiceId == Guid.Empty)
        {
            return new UpdateDraftInvoiceResponse(
                Succeeded: false,
                ErrorStatusCode: 400,
                ErrorTitle: "Validation failed",
                ErrorDetail: "InvoiceId is required.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            return new UpdateDraftInvoiceResponse(
                Succeeded: false,
                ErrorStatusCode: 400,
                ErrorTitle: "Validation failed",
                ErrorDetail: "At least one line is required.");
        }

        // Fail-fast request validation (keep domain invariants, but give the API clean errors for common input issues).
        var errors = new List<string>();
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var l = request.Lines[i];
            if (l.ProductId == Guid.Empty) errors.Add($"Lines[{i}].ProductId is required.");
            if (string.IsNullOrWhiteSpace(l.Description)) errors.Add($"Lines[{i}].Description is required.");
            if (l.Quantity <= 0) errors.Add($"Lines[{i}].Quantity must be > 0.");
            if (l.UnitPrice < 0) errors.Add($"Lines[{i}].UnitPrice must be >= 0.");
        }

        if (errors.Count > 0)
        {
            return new UpdateDraftInvoiceResponse(
                Succeeded: false,
                ErrorStatusCode: 400,
                ErrorTitle: "Validation failed",
                ErrorDetail: string.Join(Environment.NewLine, errors));
        }

        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            return new UpdateDraftInvoiceResponse(
                Succeeded: false,
                ErrorStatusCode: 404,
                ErrorTitle: "Invoice not found",
                ErrorDetail: $"Invoice {request.InvoiceId} was not found.");
        }

        if (!string.Equals(invoice.Status, InvoiceStatus.Draft, StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateDraftInvoiceResponse(
                Succeeded: false,
                ErrorStatusCode: 409,
                ErrorTitle: "Only draft invoices can be updated",
                ErrorDetail: $"Invoice {request.InvoiceId} is not in Draft status.");
        }

        // DB-level product existence check (avoid FK violations and give client a clear 400).
        var productIds = request.Lines
            .Select(x => x.ProductId)
            .Distinct()
            .ToArray();

        var existingProductIds = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var missing = productIds.Except(existingProductIds).ToArray();
        if (missing.Length > 0)
        {
            return new UpdateDraftInvoiceResponse(
                Succeeded: false,
                ErrorStatusCode: 400,
                ErrorTitle: "Validation failed",
                ErrorDetail: $"Unknown ProductId(s): {string.Join(", ", missing)}");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Delete then rebuild: stable, repeatable, and avoids tracked-graph concurrency exceptions.
        await _db.InvoiceLines
            .Where(l => l.InvoiceId == invoice.Id)
            .ExecuteDeleteAsync(cancellationToken);

        invoice.UpdateDraftHeader(request.DueDate, request.CurrencyCode, request.TaxRatePercent);
        invoice.ReplaceLines(request.Lines.Select(l => (l.ProductId, l.Description, l.UnitPrice, l.Quantity)));

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new UpdateDraftInvoiceResponse(Succeeded: true, Invoice: invoice);
    }
}
