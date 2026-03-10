using System.Globalization;

namespace InvoiceBilling.Infrastructure.PdfTemplates;

/// <summary>
/// Resolves template field keys to string values from the Invoice aggregate.
/// Customer fields are resolved via invoice.Customer (ensure it is eagerly loaded).
/// </summary>
public sealed class InvoiceValueResolver
{
    // Cached per-resolve call so currency is consistent across all fields and line cells.
    private string _currency = "INR";

    public string Resolve(object invoice, string key)
    {
        if (invoice is null) return string.Empty;
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        // Cache currency from the invoice so money fields and line cells are consistent.
        _currency = GetString(invoice, "CurrencyCode") ?? "INR";

        // Literal text prefix  e.g. "text:INVOICE"
        if (key.StartsWith("text:", StringComparison.OrdinalIgnoreCase))
            return key.Substring("text:".Length);

        // ── Invoice header ──────────────────────────────────────────────────
        if (key.Equals("invoiceNumber", StringComparison.OrdinalIgnoreCase))
            return GetString(invoice, "InvoiceNumber", "Number", "Code", "Reference") ?? string.Empty;

        if (key.Equals("issueDate", StringComparison.OrdinalIgnoreCase))
            return FormatDate(GetDate(invoice, "IssueDate", "IssuedAt", "IssuedAtUtc"));

        if (key.Equals("dueDate", StringComparison.OrdinalIgnoreCase))
            return FormatDate(GetDate(invoice, "DueDate", "DueDateUtc"));

        if (key.Equals("status", StringComparison.OrdinalIgnoreCase))
            return GetString(invoice, "Status") ?? string.Empty;

        if (key.Equals("currencyCode", StringComparison.OrdinalIgnoreCase))
            return _currency;

        // ── Customer fields (resolved via invoice.Customer) ─────────────────
        if (key.Equals("customerName", StringComparison.OrdinalIgnoreCase))
        {
            var direct = GetString(invoice, "CustomerName");
            if (!string.IsNullOrWhiteSpace(direct)) return direct;
            var cust = GetObject(invoice, "Customer");
            return cust is null ? string.Empty
                : GetString(cust, "BusinessName", "Name", "DisplayName") ?? string.Empty;
        }

        if (key.Equals("customerAddress", StringComparison.OrdinalIgnoreCase))
        {
            var cust = GetObject(invoice, "Customer");
            return cust is null ? string.Empty
                : GetString(cust, "BillingAddress", "Address") ?? string.Empty;
        }

        // Combined lines — return "Label: value" or empty so the field simply disappears when blank.
        if (key.Equals("customerEmailLine", StringComparison.OrdinalIgnoreCase))
        {
            var cust = GetObject(invoice, "Customer");
            var val = cust is null ? null : GetString(cust, "Email");
            return string.IsNullOrWhiteSpace(val) ? string.Empty : $"Email: {val}";
        }

        if (key.Equals("customerPhoneLine", StringComparison.OrdinalIgnoreCase))
        {
            var cust = GetObject(invoice, "Customer");
            var val = cust is null ? null : GetString(cust, "Phone");
            return string.IsNullOrWhiteSpace(val) ? string.Empty : $"Phone: {val}";
        }

        if (key.Equals("customerTaxIdLine", StringComparison.OrdinalIgnoreCase))
        {
            var cust = GetObject(invoice, "Customer");
            var val = cust is null ? null : GetString(cust, "TaxId");
            return string.IsNullOrWhiteSpace(val) ? string.Empty : $"Tax ID: {val}";
        }

        // ── Money totals ────────────────────────────────────────────────────
        if (key.Equals("subtotal", StringComparison.OrdinalIgnoreCase))
            return FormatMoney(GetDecimal(invoice, "Subtotal", "SubtotalAmount"));

        // "taxLabel" emits e.g. "Tax (18%):" dynamically
        if (key.Equals("taxLabel", StringComparison.OrdinalIgnoreCase))
        {
            var rate = GetDecimal(invoice, "TaxRatePercent", "TaxRate") ?? 0m;
            return $"Tax ({rate:0.##}%):";
        }

        if (key.Equals("taxTotal", StringComparison.OrdinalIgnoreCase))
            return FormatMoney(GetDecimal(invoice, "TaxTotal", "TaxAmount"));

        if (key.Equals("total", StringComparison.OrdinalIgnoreCase)
            || key.Equals("totalAmount", StringComparison.OrdinalIgnoreCase)
            || key.Equals("grandTotal", StringComparison.OrdinalIgnoreCase))
            return FormatMoney(GetDecimal(invoice, "GrandTotal", "TotalAmount", "Total") ?? ComputeLinesTotal(invoice));

        if (key.Equals("paidTotal", StringComparison.OrdinalIgnoreCase))
            return FormatMoney(GetDecimal(invoice, "PaidTotal", "AmountPaid"));

        if (key.Equals("balanceDue", StringComparison.OrdinalIgnoreCase))
            return FormatMoney(GetDecimal(invoice, "BalanceDue", "Outstanding", "Due"));

        // ── Utility ─────────────────────────────────────────────────────────
        if (key.Equals("generatedAt", StringComparison.OrdinalIgnoreCase))
            return $"Generated at {DateTime.UtcNow:O}";

        // Fallback: direct PascalCase property lookup
        var pascal = char.ToUpperInvariant(key[0]) + key.Substring(1);
        return GetString(invoice, pascal, key) ?? string.Empty;
    }

    public IReadOnlyList<object> GetLines(object invoice)
    {
        var lines = GetObject(invoice, "Lines", "InvoiceLines", "Items");
        if (lines is null) return Array.Empty<object>();
        if (lines is IEnumerable<object> objEnum) return objEnum.ToList();
        if (lines is System.Collections.IEnumerable enumAny)
        {
            var list = new List<object>();
            foreach (var it in enumAny) if (it is not null) list.Add(it);
            return list;
        }
        return Array.Empty<object>();
    }

    public string ResolveLine(object line, string key)
    {
        var k = key.Trim();
        if (k.StartsWith("line.", StringComparison.OrdinalIgnoreCase))
            k = k.Substring("line.".Length);

        if (k.Equals("description", StringComparison.OrdinalIgnoreCase))
            return GetString(line, "Description", "Name", "Title") ?? string.Empty;

        if (k.Equals("quantity", StringComparison.OrdinalIgnoreCase)
            || k.Equals("qty", StringComparison.OrdinalIgnoreCase))
        {
            var qty = GetDecimal(line, "Quantity", "Qty") ?? 0m;
            return qty % 1 == 0
                ? ((int)qty).ToString(CultureInfo.InvariantCulture)
                : qty.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // Rate / Unit / UnitPrice
        if (k.Equals("unitPrice", StringComparison.OrdinalIgnoreCase)
            || k.Equals("price", StringComparison.OrdinalIgnoreCase)
            || k.Equals("rate", StringComparison.OrdinalIgnoreCase)
            || k.Equals("unit", StringComparison.OrdinalIgnoreCase))
            return FormatMoney(GetDecimal(line, "UnitPrice", "Price"));

        // Amount / Total / LineTotal
        if (k.Equals("total", StringComparison.OrdinalIgnoreCase)
            || k.Equals("lineTotal", StringComparison.OrdinalIgnoreCase)
            || k.Equals("amount", StringComparison.OrdinalIgnoreCase))
        {
            var explicitTotal = GetDecimal(line, "LineTotal", "Total");
            if (explicitTotal.HasValue) return FormatMoney(explicitTotal);
            var qty = GetDecimal(line, "Quantity", "Qty") ?? 0m;
            var price = GetDecimal(line, "UnitPrice", "Price") ?? 0m;
            return FormatMoney(qty * price);
        }

        return GetString(line, k) ?? string.Empty;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private decimal ComputeLinesTotal(object invoice)
    {
        decimal total = 0m;
        foreach (var line in GetLines(invoice))
        {
            var qty   = GetDecimal(line, "Quantity", "Qty") ?? 0m;
            var price = GetDecimal(line, "UnitPrice", "Price") ?? 0m;
            total += qty * price;
        }
        return total;
    }

    private static string FormatDate(DateTimeOffset? dto) =>
        dto.HasValue ? dto.Value.ToLocalTime().ToString("yyyy-MM-dd") : string.Empty;

    private static string FormatDate(DateTime? dt) =>
        dt.HasValue ? dt.Value.ToString("yyyy-MM-dd") : string.Empty;

    private string FormatMoney(decimal? value)
    {
        if (!value.HasValue) return string.Empty;
        return $"{_currency} {value.Value:0.00}";
    }

    private static string? GetString(object obj, params string[] names)
    {
        foreach (var n in names)
        {
            var p = obj.GetType().GetProperty(n);
            if (p is null) continue;
            var v = p.GetValue(obj);
            if (v is null) continue;
            return v.ToString();
        }
        return null;
    }

    private static DateTimeOffset? GetDate(object obj, params string[] names)
    {
        foreach (var n in names)
        {
            var p = obj.GetType().GetProperty(n);
            if (p is null) continue;
            var v = p.GetValue(obj);
            if (v is null) continue;
            if (v is DateTimeOffset dto) return dto;
            if (v is DateTime dt)        return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
        }
        return null;
    }

    private static decimal? GetDecimal(object obj, params string[] names)
    {
        foreach (var n in names)
        {
            var p = obj.GetType().GetProperty(n);
            if (p is null) continue;
            var v = p.GetValue(obj);
            if (v is null) continue;
            if (v is decimal d)  return d;
            if (v is double db)  return (decimal)db;
            if (v is float f)    return (decimal)f;
            if (decimal.TryParse(v.ToString(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var parsed)) return parsed;
        }
        return null;
    }

    private static object? GetObject(object obj, params string[] names)
    {
        foreach (var n in names)
        {
            var p = obj.GetType().GetProperty(n);
            if (p is null) continue;
            var v = p.GetValue(obj);
            if (v is not null) return v;
        }
        return null;
    }
}
