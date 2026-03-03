using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Text.Json;

namespace InvoiceBilling.Infrastructure.PdfTemplates;

public sealed class InvoicePdfTemplateRenderer : IInvoicePdfTemplateRenderer
{
    private readonly InvoiceValueResolver _resolver = new();

    public byte[] Render(object invoice, PdfTemplateDefinition template)
    {
        var doc = new PdfDocument();
        var page = doc.AddPage();

        var widthPt = MmToPt(template.Page.WidthMm);
        var heightPt = MmToPt(template.Page.HeightMm);
        page.Width = XUnit.FromPoint(widthPt);
        page.Height = XUnit.FromPoint(heightPt);

        using var gfx = XGraphics.FromPdfPage(page);

        // Free positioned fields
        foreach (var f in template.Fields ?? new List<PdfTemplateField>())
        {
            var value = _resolver.Resolve(invoice, f.Key);
            var x = MmToPt(f.Xmm);
            var y = MmToPt(f.Ymm);
            var font = ToXFont(f.Font);
            var brush = ToBrush(f.Color);
            gfx.DrawString(value, font, brush, new XPoint(x, y));
        }

        // Lines table
        if (template.LinesTable is not null && template.LinesTable.Columns.Count > 0)
        {
            RenderLinesTable(gfx, invoice, template);
        }

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    public static PdfTemplateDefinition ParseTemplate(JsonElement json)
        => JsonSerializer.Deserialize<PdfTemplateDefinition>(json.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new PdfTemplateDefinition();

    private void RenderLinesTable(XGraphics gfx, object invoice, PdfTemplateDefinition template)
    {
        var table = template.LinesTable!;
        var xStart = MmToPt(table.Xmm);
        var yStart = MmToPt(table.Ymm);
        var rowH = MmToPt(table.RowHeightMm);

        var colWidths = table.Columns.Select(c => MmToPt(c.WidthMm)).ToArray();
        var headerFont = ToXFont(table.Header.Font);
        var cellFont = ToXFont(new PdfFontSpec { Family = table.Header.Font.Family, Size = Math.Max(8, table.Header.Font.Size - 1) });

        var y = yStart;
        if (table.Header.Enabled)
        {
            double x = xStart;
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var col = table.Columns[i];
                gfx.DrawString(col.Header, headerFont, XBrushes.Black, new XRect(x, y, colWidths[i], rowH), XStringFormats.TopLeft);
                x += colWidths[i];
            }
            y += rowH;
        }

        var lines = _resolver.GetLines(invoice);
        foreach (var line in lines)
        {
            double x = xStart;
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var col = table.Columns[i];
                var text = _resolver.ResolveLine(line, col.Key);
                gfx.DrawString(text, cellFont, XBrushes.Black, new XRect(x, y, colWidths[i], rowH), XStringFormats.TopLeft);
                x += colWidths[i];
            }
            y += rowH;
        }
    }

    private static XFont ToXFont(PdfFontSpec spec)
    {
        var style = XFontStyleEx.Regular;
        if (spec.Bold) style |= XFontStyleEx.Bold;
        if (spec.Italic) style |= XFontStyleEx.Italic;
        return new XFont(spec.Family, spec.Size, style);
    }

    private static XBrush ToBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return XBrushes.Black;
        try
        {
            var h = hex.Trim();
            if (h.StartsWith('#')) h = h.Substring(1);
            if (h.Length == 6)
            {
                var r = Convert.ToInt32(h.Substring(0, 2), 16);
                var g = Convert.ToInt32(h.Substring(2, 2), 16);
                var b = Convert.ToInt32(h.Substring(4, 2), 16);
                return new XSolidBrush(XColor.FromArgb(r, g, b));
            }
        }
        catch
        {
            // ignore
        }
        return XBrushes.Black;
    }

    private static double MmToPt(double mm) => mm * 72.0 / 25.4;
}

