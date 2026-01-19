using MediatR;

namespace InvoiceBilling.Application.Invoices.GetInvoiceStatus;

/// <summary>
/// CQRS query: Lightweight invoice state for UI polling (status + PDF readiness).
/// </summary>
public sealed record GetInvoiceStatusQuery(Guid InvoiceId) : IRequest<GetInvoiceStatusResponse>;

public sealed record GetInvoiceStatusResponse(
    bool Succeeded,
    InvoiceStatusState? State = null,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);

public sealed record InvoiceStatusState(
    Guid Id,
    string RawStatus,
    string EffectiveStatus,
    DateTime DueDate,
    decimal PaidTotal,
    decimal BalanceDue,
    string? PdfS3Key
);
