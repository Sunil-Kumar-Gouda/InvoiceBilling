using MediatR;

namespace InvoiceBilling.Application.PdfTemplates.SaveActivePdfTemplate;

/// <summary>
/// CQRS command: Upserts the active PDF template from raw JSON.
/// Does not return data — callers check Succeeded for error handling.
/// </summary>
public sealed record SaveActivePdfTemplateCommand(
    string TemplateJson
) : IRequest<SaveActivePdfTemplateResponse>;

public sealed record SaveActivePdfTemplateResponse(
    bool Succeeded,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);
