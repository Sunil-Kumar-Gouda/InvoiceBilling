using MediatR;

namespace InvoiceBilling.Application.PdfTemplates.PreviewInvoicePdf;

/// <summary>
/// CQRS query: Load an invoice with its Customer and Lines, render it using the
/// provided template JSON, and return the raw PDF bytes.
/// Does NOT persist the template.
/// </summary>
public sealed record PreviewInvoicePdfQuery(
    Guid InvoiceId,
    string TemplateJson
) : IRequest<PreviewInvoicePdfResponse>;

public sealed record PreviewInvoicePdfResponse(
    bool Succeeded,
    byte[]? PdfBytes = null,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);
