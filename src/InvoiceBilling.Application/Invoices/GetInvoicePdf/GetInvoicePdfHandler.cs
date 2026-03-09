using InvoiceBilling.Application.Common.Persistence;
using InvoiceBilling.Application.Common.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Application.Invoices.GetInvoicePdf;

public sealed class GetInvoicePdfHandler : IRequestHandler<GetInvoicePdfQuery, GetInvoicePdfResponse>
{
    private readonly IInvoiceBillingDbContext _db;
    private readonly IInvoicePdfStorage _pdfStorage;

    public GetInvoicePdfHandler(IInvoiceBillingDbContext db, IInvoicePdfStorage pdfStorage)
    {
        _db = db;
        _pdfStorage = pdfStorage;
    }

    public async Task<GetInvoicePdfResponse> Handle(GetInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
            return Fail(404, "Invoice not found", $"Invoice {request.InvoiceId} was not found.");

        if (string.IsNullOrWhiteSpace(invoice.PdfS3Key))
            return Fail(409, "PDF not ready",
                "Invoice file not generated yet. Please issue the invoice and wait for the worker.");

        var download = await _pdfStorage.TryDownloadAsync(invoice.PdfS3Key, invoice.InvoiceNumber, cancellationToken);

        if (download is null)
            return Fail(404, "PDF not found",
                "Invoice file not found in storage. Re-issue the invoice or re-run the worker.");

        return new GetInvoicePdfResponse(
            Succeeded: true,
            ContentStream: download.ContentStream,
            ContentType: download.ContentType,
            FileName: download.FileName);
    }

    private static GetInvoicePdfResponse Fail(int statusCode, string title, string detail) =>
        new(Succeeded: false, ErrorStatusCode: statusCode, ErrorTitle: title, ErrorDetail: detail);
}
