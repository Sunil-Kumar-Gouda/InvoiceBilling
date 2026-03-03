namespace InvoiceBilling.Infrastructure.PdfTemplates;

public interface IInvoicePdfTemplateRenderer
{
    /// <summary>
    /// Renders an invoice PDF using the provided template definition.
    /// </summary>
    byte[] Render(object invoice, PdfTemplateDefinition template);
}

