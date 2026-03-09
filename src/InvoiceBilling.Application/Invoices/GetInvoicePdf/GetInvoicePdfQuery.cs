using MediatR;

namespace InvoiceBilling.Application.Invoices.GetInvoicePdf;

/// <summary>
/// CQRS query: download the generated PDF for an issued invoice from object storage.
/// </summary>
public sealed record GetInvoicePdfQuery(Guid InvoiceId) : IRequest<GetInvoicePdfResponse>;

public sealed record GetInvoicePdfResponse(
    bool Succeeded,
    Stream? ContentStream = null,
    string? ContentType = null,
    string? FileName = null,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);
