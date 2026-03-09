using MediatR;

namespace InvoiceBilling.Application.Invoices.GetInvoicePayments;

/// <summary>
/// CQRS query: retrieve all payments recorded against an invoice.
/// </summary>
public sealed record GetInvoicePaymentsQuery(Guid InvoiceId) : IRequest<GetInvoicePaymentsResponse>;

public sealed record GetInvoicePaymentsResponse(
    bool Succeeded,
    IReadOnlyList<PaymentResult>? Payments = null,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);

public sealed record PaymentResult(
    Guid Id,
    Guid InvoiceId,
    decimal Amount,
    DateTime PaidAt,
    string? Method,
    string? Reference,
    string? Note,
    DateTime CreatedAt
);
