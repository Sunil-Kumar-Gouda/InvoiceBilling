namespace InvoiceBilling.Application.Common.PdfTemplates;

/// <summary>
/// Renders a PDF preview for an invoice using a raw template JSON string.
/// Defined in the Application layer so handlers remain independent of Infrastructure.
/// Implemented in InvoiceBilling.Infrastructure.
/// </summary>
public interface IInvoicePdfPreviewRenderer
{
    byte[] RenderPreview(object invoice, string templateJson);
}
