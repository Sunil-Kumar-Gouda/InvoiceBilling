using InvoiceBilling.Domain.Entities;
using MediatR;

namespace InvoiceBilling.Application.Invoices.IssueInvoice;

/// <summary>
/// CQRS command: Issue an invoice (Draft -> Issued) and enqueue PDF generation (best-effort).
/// </summary>
public sealed record IssueInvoiceCommand(
    Guid InvoiceId,
    DateTime? IssuedAtUtc = null
) : IRequest<IssueInvoiceResponse>;

public sealed record IssueInvoiceResponse(
    bool Succeeded,
    Invoice? Invoice = null,
    bool JobEnqueued = false,
    string? JobEnqueueError = null,
    bool WasNoOp = false,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);
