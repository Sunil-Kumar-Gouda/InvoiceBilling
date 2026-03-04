using InvoiceBilling.Application.Common.PdfTemplates;
using InvoiceBilling.Application.Common.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Application.PdfTemplates.PreviewInvoicePdf;

public sealed class PreviewInvoicePdfHandler
    : IRequestHandler<PreviewInvoicePdfQuery, PreviewInvoicePdfResponse>
{
    private readonly IInvoiceBillingDbContext _db;
    private readonly IInvoicePdfPreviewRenderer _renderer;

    public PreviewInvoicePdfHandler(IInvoiceBillingDbContext db, IInvoicePdfPreviewRenderer renderer)
    {
        _db = db;
        _renderer = renderer;
    }

    public async Task<PreviewInvoicePdfResponse> Handle(
        PreviewInvoicePdfQuery request,
        CancellationToken cancellationToken)
    {
        // Load Invoice with Customer (reference) and Lines (collection).
        // AsSplitQuery avoids a cartesian explosion between the Lines collection
        // and the Customer join — each Include runs as its own focused query.
        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
            return new PreviewInvoicePdfResponse(
                Succeeded: false,
                ErrorStatusCode: 404,
                ErrorTitle: "Invoice not found",
                ErrorDetail: $"Invoice {request.InvoiceId} was not found.");

        var pdfBytes = _renderer.RenderPreview(invoice, request.TemplateJson);

        return new PreviewInvoicePdfResponse(
            Succeeded: true,
            PdfBytes: pdfBytes);
    }
}
