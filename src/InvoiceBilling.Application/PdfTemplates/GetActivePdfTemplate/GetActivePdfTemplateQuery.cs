using MediatR;

namespace InvoiceBilling.Application.PdfTemplates.GetActivePdfTemplate;

/// <summary>
/// CQRS query: Returns the active PDF template JSON, or a 404 response if none exists.
/// </summary>
public sealed record GetActivePdfTemplateQuery() : IRequest<GetActivePdfTemplateResponse>;

public sealed record GetActivePdfTemplateResponse(
    bool Succeeded,
    string? TemplateJson = null,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);
