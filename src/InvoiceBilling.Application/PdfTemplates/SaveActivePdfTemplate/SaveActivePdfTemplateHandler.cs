using InvoiceBilling.Application.Common.PdfTemplates;
using MediatR;

namespace InvoiceBilling.Application.PdfTemplates.SaveActivePdfTemplate;

public sealed class SaveActivePdfTemplateHandler
    : IRequestHandler<SaveActivePdfTemplateCommand, SaveActivePdfTemplateResponse>
{
    private readonly IActivePdfTemplateStore _store;

    public SaveActivePdfTemplateHandler(IActivePdfTemplateStore store)
    {
        _store = store;
    }

    public async Task<SaveActivePdfTemplateResponse> Handle(
        SaveActivePdfTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateJson))
            return new SaveActivePdfTemplateResponse(
                Succeeded: false,
                ErrorStatusCode: 400,
                ErrorTitle: "Validation failed",
                ErrorDetail: "Template JSON must not be empty.");

        await _store.SaveActiveTemplateJsonAsync(request.TemplateJson, cancellationToken);

        return new SaveActivePdfTemplateResponse(Succeeded: true);
    }
}
