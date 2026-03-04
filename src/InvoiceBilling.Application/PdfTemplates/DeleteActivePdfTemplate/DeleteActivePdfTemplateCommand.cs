using MediatR;

namespace InvoiceBilling.Application.PdfTemplates.DeleteActivePdfTemplate;

/// <summary>
/// CQRS command: Deletes the active PDF template, reverting to the built-in layout.
/// </summary>
public sealed record DeleteActivePdfTemplateCommand() : IRequest<DeleteActivePdfTemplateResponse>;

public sealed record DeleteActivePdfTemplateResponse(
    bool Succeeded,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);
