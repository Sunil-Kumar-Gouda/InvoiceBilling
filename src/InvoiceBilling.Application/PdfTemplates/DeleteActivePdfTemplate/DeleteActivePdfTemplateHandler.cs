using InvoiceBilling.Application.Common.PdfTemplates;
using MediatR;

namespace InvoiceBilling.Application.PdfTemplates.DeleteActivePdfTemplate;

public sealed class DeleteActivePdfTemplateHandler
    : IRequestHandler<DeleteActivePdfTemplateCommand, DeleteActivePdfTemplateResponse>
{
    private readonly IActivePdfTemplateStore _store;

    public DeleteActivePdfTemplateHandler(IActivePdfTemplateStore store)
    {
        _store = store;
    }

    public async Task<DeleteActivePdfTemplateResponse> Handle(
        DeleteActivePdfTemplateCommand request,
        CancellationToken cancellationToken)
    {
        await _store.DeleteActiveTemplateAsync(cancellationToken);

        return new DeleteActivePdfTemplateResponse(Succeeded: true);
    }
}
