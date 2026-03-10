using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using System.Text.Json;

namespace InvoiceBilling.Infrastructure.PdfTemplates;

public sealed class InvoicePdfTemplateRenderer : IInvoicePdfTemplateRenderer
{
    private static readonly Lazy<IFontResolver> FontResolver =
        new(() => new FailsafeFontResolver());

    private readonly InvoiceValueResolver _resolver = new();

    public byte[] Render(object invoice, PdfTemplateDefinition template)
    {
        if (Capabilities.Build.IsCoreBuild && GlobalFontSettings.FontResolver is null)
            GlobalFontSettings.FontResolver = FontResolver.Value;

        var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Width  = XUnit.FromPoint(template.Page.Width);
        page.Height = XUnit.FromPoint(template.Page.Height);

        using var gfx = XGraphics.FromPdfPage(page);

        // Free-positioned fields
        foreach (var f in template.Fields ?? new List<PdfTemplateField>())
        {
            var value = _resolver.Resolve(invoice, f.Key);
            var font  = ToXFont(f.Font);
            var brush = ToBrush(f.Color);

            if (f.W > 0 && f.H > 0)
            {
                var fmt = ToStringFormat(f.Align);
                gfx.DrawString(value, font, brush, new XRect(f.X, f.Y, f.W, f.H), fmt);
            }
            else
            {
                // Fallback: point-based (baseline) when no bounding box supplied
                gfx.DrawString(value, font, brush, new XPoint(f.X, f.Y));
            }
        }

        // Lines table
        if (template.LinesTable is not null && template.LinesTable.Columns.Count > 0)
            RenderLinesTable(gfx, invoice, template);

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
        var table     = template.LinesTable!;
        var xStart    = table.X;
        var yStart    = table.Y;
        var tableEndX = table.X + table.W;
        var rowH      = table.RowHeight > 0 ? table.RowHeight : 16;
        var maxY      = yStart + table.H;

        var colWidths  = table.Columns.Select(c => c.W).ToArray();
        var headerFont = ToXFont(table.HeaderFont);
        var cellFont   = ToXFont(table.RowFont);

        var y = yStart;

        // Line 1: above the header row
        gfx.DrawLine(XPens.Black, xStart, y, tableEndX, y);
        y += 4;

        // Header row
        double x = xStart;
        for (var i = 0; i < table.Columns.Count; i++)
        {
            var col   = table.Columns[i];
            var label = string.IsNullOrWhiteSpace(col.Header) ? col.Key : col.Header;
            gfx.DrawString(label, headerFont, XBrushes.Black,
                new XRect(x, y, colWidths[i], rowH), ToStringFormat(col.Align));
            x += colWidths[i];
        }
        y += rowH;

        // Line 2: below the header row
        gfx.DrawLine(XPens.Black, xStart, y, tableEndX, y);
        y += 4;

        // Data rows
        foreach (var line in _resolver.GetLines(invoice))
        {
            if (y + rowH > maxY) break;

            x = xStart;
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var col  = table.Columns[i];
                var text = _resolver.ResolveLine(line, col.Key);
                gfx.DrawString(text, cellFont, XBrushes.Black,
                    new XRect(x, y, colWidths[i], rowH), ToStringFormat(col.Align));
                x += colWidths[i];
            }
            y += rowH;
        }

        // Line 3: below all data rows
        y += 4;
        gfx.DrawLine(XPens.Black, xStart, y, tableEndX, y);
    }

    private static XStringFormat ToStringFormat(string? align) =>
        align?.Trim().ToUpperInvariant() switch
        {
            "RIGHT"  => XStringFormats.TopRight,
            "CENTER" => XStringFormats.TopCenter,
            _        => XStringFormats.TopLeft
        };

    private static XFont ToXFont(PdfFontSpec spec)
    {
        var style = XFontStyleEx.Regular;
        if (spec.Bold)   style |= XFontStyleEx.Bold;
        if (spec.Italic) style |= XFontStyleEx.Italic;
        return new XFont(spec.Family, spec.Size, style);
    }

    private static XBrush ToBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return XBrushes.Black;
        try
        {
            var h = hex.Trim().TrimStart('#');
            if (h.Length == 6)
            {
                var r = Convert.ToInt32(h.Substring(0, 2), 16);
                var g = Convert.ToInt32(h.Substring(2, 2), 16);
                var b = Convert.ToInt32(h.Substring(4, 2), 16);
                return new XSolidBrush(XColor.FromArgb(r, g, b));
            }
        }
        catch { /* ignore malformed color */ }
        return XBrushes.Black;
    }
}
