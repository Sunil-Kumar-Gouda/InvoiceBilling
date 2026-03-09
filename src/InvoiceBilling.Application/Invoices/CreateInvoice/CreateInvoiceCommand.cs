using InvoiceBilling.Domain.Entities;
using MediatR;

namespace InvoiceBilling.Application.Invoices.CreateInvoice;

/// <summary>
/// CQRS command: create a new Draft invoice.
/// </summary>
public sealed record CreateInvoiceCommand(
    Guid CustomerId,
    DateTime? IssueDate,
    DateTime? DueDate,
    string? CurrencyCode,
    IReadOnlyList<CreateInvoiceLine> Lines
) : IRequest<CreateInvoiceResponse>;

public sealed record CreateInvoiceLine(
    Guid ProductId,
    string? Description,
    decimal UnitPrice,
    decimal Quantity
);

public sealed record CreateInvoiceResponse(
    bool Succeeded,
    Invoice? Invoice = null,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);
