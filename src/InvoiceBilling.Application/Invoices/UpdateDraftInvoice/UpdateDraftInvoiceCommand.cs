using InvoiceBilling.Domain.Entities;
using MediatR;

namespace InvoiceBilling.Application.Invoices.UpdateDraftInvoice;

/// <summary>
/// CQRS command: update a Draft invoice in an idempotent way.
///
/// The handler deletes all existing invoice lines set-based (by InvoiceId)
/// and then rebuilds the invoice lines from the request payload.
/// This avoids EF Core tracked-graph double-delete issues that can surface as
/// DbUpdateConcurrencyException on InvoiceLine.
/// </summary>
public sealed record UpdateDraftInvoiceCommand(
    Guid InvoiceId,
    DateTime DueDate,
    string CurrencyCode,
    decimal TaxRatePercent,
    IReadOnlyList<UpdateDraftInvoiceLine> Lines
) : IRequest<UpdateDraftInvoiceResponse>;

public sealed record UpdateDraftInvoiceLine(
    Guid ProductId,
    string Description,
    decimal UnitPrice,
    decimal Quantity
);

public sealed record UpdateDraftInvoiceResponse(
    bool Succeeded,
    Invoice? Invoice = null,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);
