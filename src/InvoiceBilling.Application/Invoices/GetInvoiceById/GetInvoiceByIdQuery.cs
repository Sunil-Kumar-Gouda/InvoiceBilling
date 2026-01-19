using MediatR;

namespace InvoiceBilling.Application.Invoices.GetInvoiceById;

/// <summary>
/// CQRS query: Load a single invoice with lines.
/// </summary>
public sealed record GetInvoiceByIdQuery(Guid InvoiceId) : IRequest<GetInvoiceByIdResponse>;

public sealed record GetInvoiceByIdResponse(
    bool Succeeded,
    InvoiceDetails? Invoice = null,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);

public sealed record InvoiceDetails(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string Status,
    DateTime IssueDate,
    DateTime DueDate,
    string CurrencyCode,
    decimal TaxRatePercent,
    decimal Subtotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal PaidTotal,
    decimal BalanceDue,
    string? PdfS3Key,
    DateTime CreatedAt,
    IReadOnlyList<InvoiceLineDetails> Lines
);

public sealed record InvoiceLineDetails(
    Guid Id,
    Guid ProductId,
    string Description,
    decimal UnitPrice,
    decimal Quantity,
    decimal LineTotal
);
