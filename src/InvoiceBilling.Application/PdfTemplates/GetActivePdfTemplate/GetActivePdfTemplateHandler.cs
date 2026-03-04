using InvoiceBilling.Application.Common.PdfTemplates;
using MediatR;

namespace InvoiceBilling.Application.PdfTemplates.GetActivePdfTemplate;

public sealed class GetActivePdfTemplateHandler
    : IRequestHandler<GetActivePdfTemplateQuery, GetActivePdfTemplateResponse>
{
    private readonly IActivePdfTemplateStore _store;

    public GetActivePdfTemplateHandler(IActivePdfTemplateStore store)
    {
        _store = store;
    }

    public async Task<GetActivePdfTemplateResponse> Handle(
        GetActivePdfTemplateQuery request,
        CancellationToken cancellationToken)
    {
        var json = await _store.GetActiveTemplateJsonAsync(cancellationToken);

        if (json is null)
            return new GetActivePdfTemplateResponse(
                Succeeded: false,
                ErrorStatusCode: 404,
                ErrorTitle: "Template not found",
                ErrorDetail: "No active PDF template has been saved yet.");

        return new GetActivePdfTemplateResponse(
            Succeeded: true,
            TemplateJson: json);
    }
}
