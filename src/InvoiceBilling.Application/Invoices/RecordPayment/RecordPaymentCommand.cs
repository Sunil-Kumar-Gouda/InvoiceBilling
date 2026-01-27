using InvoiceBilling.Domain.Entities;
using MediatR;

namespace InvoiceBilling.Application.Invoices.RecordPayment;

/// <summary>
/// Record a payment against an issued invoice.
/// </summary>
public sealed record RecordPaymentCommand(
    Guid InvoiceId,
    decimal Amount,
    DateTime PaidAtUtc,
    string? Method,
    string? Reference,
    string? Note
) : IRequest<RecordPaymentResponse>;

public sealed record RecordPaymentResponse(
    bool Succeeded,
    Invoice? Invoice = null,
    Payment? Payment = null,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);
