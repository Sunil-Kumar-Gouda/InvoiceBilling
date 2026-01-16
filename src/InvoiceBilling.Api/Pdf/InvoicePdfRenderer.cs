using System.Globalization;
using InvoiceBilling.Domain.Entities;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace InvoiceBilling.Api.Pdf;

/// <summary>
/// Minimal invoice PDF renderer using PDFsharp.
/// Layout is intentionally simple so we can later evolve to templating.
/// </summary>
public static class InvoicePdfRenderer
{
    // GlobalFontSettings requires the same instance if set multiple times.
    private static readonly Lazy<IFontResolver> FontResolver = new(() => new FailsafeFontResolver());

    public static byte[] Render(Invoice invoice, Customer? customer)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));

        // PDFsharp Core build needs font resolving configured.
        // See PDFsharp docs: set a font resolver before creating XFont objects.
        if (Capabilities.Build.IsCoreBuild && GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = FontResolver.Value;
        }

        using var doc = new PdfDocument();
        doc.Info.Title = $"Invoice {invoice.InvoiceNumber}";

        var page = doc.AddPage();
        page.Size = PageSize.A4;

        using var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
        var headerFont = new XFont("Arial", 11, XFontStyleEx.Bold);
        var bodyFont = new XFont("Arial", 10, XFontStyleEx.Regular);
        var monoFont = new XFont("Courier New", 9, XFontStyleEx.Regular);

        const double margin = 40;
        var y = margin;

        // Header
        gfx.DrawString("INVOICE", titleFont, XBrushes.Black, new XPoint(margin, y));
        y += 26;

        gfx.DrawString($"Invoice No: {invoice.InvoiceNumber}", headerFont, XBrushes.Black, new XPoint(margin, y));
        y += 16;

        gfx.DrawString($"Status: {invoice.Status}", bodyFont, XBrushes.Black, new XPoint(margin, y));
        y += 14;

        gfx.DrawString($"Issue Date (UTC): {invoice.IssueDate:yyyy-MM-dd}", bodyFont, XBrushes.Black, new XPoint(margin, y));
        y += 14;

        gfx.DrawString($"Due Date (UTC): {invoice.DueDate:yyyy-MM-dd}", bodyFont, XBrushes.Black, new XPoint(margin, y));
        y += 18;

        // Customer block
        var custName = customer?.BusinessName ?? customer?.Name ?? "Customer";

        gfx.DrawString("Bill To:", headerFont, XBrushes.Black, new XPoint(margin, y));
        y += 14;

        gfx.DrawString(custName, bodyFont, XBrushes.Black, new XPoint(margin, y));
        y += 14;

        if (!string.IsNullOrWhiteSpace(customer?.BillingAddress))
        {
            foreach (var line in SplitLines(customer!.BillingAddress!, 72))
            {
                gfx.DrawString(line, bodyFont, XBrushes.Black, new XPoint(margin, y));
                y += 12;
            }
        }

        if (!string.IsNullOrWhiteSpace(customer?.Email))
        {
            gfx.DrawString($"Email: {customer!.Email}", bodyFont, XBrushes.Black, new XPoint(margin, y));
            y += 12;
        }

        if (!string.IsNullOrWhiteSpace(customer?.Phone))
        {
            gfx.DrawString($"Phone: {customer!.Phone}", bodyFont, XBrushes.Black, new XPoint(margin, y));
            y += 12;
        }

        if (!string.IsNullOrWhiteSpace(customer?.TaxId))
        {
            gfx.DrawString($"Tax ID: {customer!.TaxId}", bodyFont, XBrushes.Black, new XPoint(margin, y));
            y += 12;
        }

        y += 14;

        // Table header
        var xDesc = margin;
        var xQty = margin + 300;
        var xUnit = margin + 350;
        var xTotal = margin + 450;

        gfx.DrawLine(XPens.Black, xDesc, y, margin + 515, y);
        y += 12;

        gfx.DrawString("Description", headerFont, XBrushes.Black, new XPoint(xDesc, y));
        gfx.DrawString("Qty", headerFont, XBrushes.Black, new XPoint(xQty, y));
        gfx.DrawString("Unit", headerFont, XBrushes.Black, new XPoint(xUnit, y));
        gfx.DrawString("Total", headerFont, XBrushes.Black, new XPoint(xTotal, y));
        y += 8;

        gfx.DrawLine(XPens.Black, xDesc, y, margin + 515, y);
        y += 12;

        // Rows
        foreach (var line in invoice.Lines)
        {
            var descLines = SplitLines(line.Description, 55);
            var rowHeight = Math.Max(1, descLines.Count) * 12;

            var rowY = y;
            foreach (var dl in descLines)
            {
                gfx.DrawString(dl, bodyFont, XBrushes.Black, new XPoint(xDesc, rowY));
                rowY += 12;
            }

            gfx.DrawString(FormatQty(line.Quantity), bodyFont, XBrushes.Black, new XPoint(xQty, y));
            gfx.DrawString(FormatMoney(invoice.CurrencyCode, line.UnitPrice), bodyFont, XBrushes.Black, new XPoint(xUnit, y));
            gfx.DrawString(FormatMoney(invoice.CurrencyCode, line.LineTotal), bodyFont, XBrushes.Black, new XPoint(xTotal, y));

            y += rowHeight + 4;
        }

        y += 10;
        gfx.DrawLine(XPens.Black, xDesc, y, margin + 515, y);
        y += 14;

        // Totals (right aligned)
        var labelX = margin + 330;
        var valueX = margin + 450;

        gfx.DrawString("Subtotal:", headerFont, XBrushes.Black, new XPoint(labelX, y));
        gfx.DrawString(FormatMoney(invoice.CurrencyCode, invoice.Subtotal), bodyFont, XBrushes.Black, new XPoint(valueX, y));
        y += 14;

        gfx.DrawString($"Tax ({invoice.TaxRatePercent:0.##}%):", headerFont, XBrushes.Black, new XPoint(labelX, y));
        gfx.DrawString(FormatMoney(invoice.CurrencyCode, invoice.TaxTotal), bodyFont, XBrushes.Black, new XPoint(valueX, y));
        y += 14;

        gfx.DrawString("Grand Total:", headerFont, XBrushes.Black, new XPoint(labelX, y));
        gfx.DrawString(FormatMoney(invoice.CurrencyCode, invoice.GrandTotal), headerFont, XBrushes.Black, new XPoint(valueX, y));
        y += 18;

        // Footer
        var footerY = page.Height.Point - margin;
        gfx.DrawLine(XPens.Gray, margin, footerY - 14, margin + 515, footerY - 14);
        gfx.DrawString($"Generated at {DateTime.UtcNow:O}", monoFont, XBrushes.Gray, new XPoint(margin, footerY - 2));

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    private static string FormatMoney(string? currencyCode, decimal value)
    {
        var cc = string.IsNullOrWhiteSpace(currencyCode) ? "INR" : currencyCode.Trim().ToUpperInvariant();
        return $"{cc} {value:0.00}";
    }

    private static string FormatQty(decimal quantity) =>
        quantity % 1 == 0
            ? ((int)quantity).ToString(CultureInfo.InvariantCulture)
            : quantity.ToString("0.###", CultureInfo.InvariantCulture);

    private static List<string> SplitLines(string text, int maxLen)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            result.Add(string.Empty);
            return result;
        }

        foreach (var raw in text.Replace("\r", string.Empty).Split('\n'))
        {
            var s = raw.Trim();
            while (s.Length > maxLen)
            {
                var cut = s.LastIndexOf(' ', maxLen);
                if (cut <= 0) cut = maxLen;
                result.Add(s[..cut].Trim());
                s = s[cut..].Trim();
            }

            if (s.Length > 0)
                result.Add(s);
        }

        return result.Count == 0 ? new List<string> { string.Empty } : result;
    }
}
