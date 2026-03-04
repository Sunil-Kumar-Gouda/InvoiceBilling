using InvoiceBilling.Application.Common.PdfTemplates;

namespace InvoiceBilling.Infrastructure.PdfTemplates;

/// <summary>
/// Infrastructure implementation of <see cref="IInvoicePdfPreviewRenderer"/>.
/// Delegates to the existing <see cref="InvoicePdfTemplateRenderer"/> so rendering
/// logic stays in one place.
/// </summary>
public sealed class InvoicePdfPreviewRenderer : IInvoicePdfPreviewRenderer
{
    private readonly IInvoicePdfTemplateRenderer _renderer;

    public InvoicePdfPreviewRenderer(IInvoicePdfTemplateRenderer renderer)
    {
        _renderer = renderer;
    }

    public byte[] RenderPreview(object invoice, string templateJson)
    {
        var def = InvoicePdfTemplateRenderer.ParseTemplate(
            System.Text.Json.JsonDocument.Parse(templateJson).RootElement);

        return _renderer.Render(invoice, def);
    }
}
