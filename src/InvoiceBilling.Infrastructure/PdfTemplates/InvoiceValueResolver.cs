using System.Globalization;

namespace InvoiceBilling.Infrastructure.PdfTemplates;

/// <summary>
/// Resolves well-known template keys into string values from your Invoice aggregate.
///
/// IMPORTANT:
/// To keep this patch applying cleanly across your evolving domain model, this resolver
/// uses reflection + multiple fallback property names.
/// You can tighten it later once the domain is stable.
/// </summary>
public sealed class InvoiceValueResolver
{
    public string Resolve(object invoice, string key)
    {
        if (invoice is null) return string.Empty;
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        if (key.StartsWith("text:", StringComparison.OrdinalIgnoreCase))
        {
            return key.Substring("text:".Length);
        }

        // Invoice header
        if (key.Equals("invoiceNumber", StringComparison.OrdinalIgnoreCase))
            return GetString(invoice, "InvoiceNumber", "Number", "Code", "Reference") ?? string.Empty;

        if (key.Equals("issueDate", StringComparison.OrdinalIgnoreCase))
            return FormatDate(GetDate(invoice, "IssuedAtUtc", "IssuedOnUtc", "IssuedAt", "IssueDate", "IssuedDate"));

        if (key.Equals("dueDate", StringComparison.OrdinalIgnoreCase))
            return FormatDate(GetDate(invoice, "DueDateUtc", "DueDate", "PaymentDueDate"));

        if (key.Equals("status", StringComparison.OrdinalIgnoreCase))
            return GetString(invoice, "Status") ?? string.Empty;

        // Customer
        if (key.Equals("customerName", StringComparison.OrdinalIgnoreCase))
        {
            var direct = GetString(invoice, "CustomerName");
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            var cust = GetObject(invoice, "Customer");
            if (cust is null) return string.Empty;
            return GetString(cust, "Name", "DisplayName", "FullName") ?? string.Empty;
        }

        // Money
        if (key.Equals("paidTotal", StringComparison.OrdinalIgnoreCase))
            return FormatMoney(GetDecimal(invoice, "PaidTotal", "AmountPaid"));

        if (key.Equals("balanceDue", StringComparison.OrdinalIgnoreCase))
            return FormatMoney(GetDecimal(invoice, "BalanceDue", "Outstanding", "Due"));

        if (key.Equals("total", StringComparison.OrdinalIgnoreCase) || key.Equals("totalAmount", StringComparison.OrdinalIgnoreCase))
            return FormatMoney(GetDecimal(invoice, "TotalAmount", "Total", "GrandTotal") ?? ComputeLinesTotal(invoice));

        // Fallback: try direct property lookup by key (camelCase -> PascalCase)
        var pascal = char.ToUpperInvariant(key[0]) + key.Substring(1);
        return GetString(invoice, pascal, key) ?? string.Empty;
    }

    public IReadOnlyList<object> GetLines(object invoice)
    {
        var lines = GetObject(invoice, "Lines", "InvoiceLines", "Items");
        if (lines is null) return Array.Empty<object>();

        if (lines is IEnumerable<object> objEnum) return objEnum.ToList();

        // non-generic IEnumerable
        if (lines is System.Collections.IEnumerable enumAny)
        {
            var list = new List<object>();
            foreach (var it in enumAny)
            {
                if (it is not null) list.Add(it);
            }
            return list;
        }

        return Array.Empty<object>();
    }

    public string ResolveLine(object line, string key)
    {
        // key examples: line.description, line.quantity, line.unitPrice, line.total
        var k = key.Trim();
        if (k.StartsWith("line.", StringComparison.OrdinalIgnoreCase)) k = k.Substring("line.".Length);

        if (k.Equals("description", StringComparison.OrdinalIgnoreCase))
            return GetString(line, "Description", "Name", "Title") ?? string.Empty;

        if (k.Equals("quantity", StringComparison.OrdinalIgnoreCase) || k.Equals("qty", StringComparison.OrdinalIgnoreCase))
            return (GetDecimal(line, "Quantity", "Qty") ?? 0m).ToString(CultureInfo.InvariantCulture);

        if (k.Equals("unitPrice", StringComparison.OrdinalIgnoreCase) || k.Equals("price", StringComparison.OrdinalIgnoreCase))
            return FormatMoney(GetDecimal(line, "UnitPrice", "Price"));

        if (k.Equals("total", StringComparison.OrdinalIgnoreCase) || k.Equals("lineTotal", StringComparison.OrdinalIgnoreCase))
        {
            var explicitTotal = GetDecimal(line, "LineTotal", "Total");
            if (explicitTotal.HasValue) return FormatMoney(explicitTotal);

            var qty = GetDecimal(line, "Quantity", "Qty") ?? 0m;
            var price = GetDecimal(line, "UnitPrice", "Price") ?? 0m;
            return FormatMoney(qty * price);
        }

        return GetString(line, k) ?? string.Empty;
    }

    private static decimal ComputeLinesTotal(object invoice)
    {
        var lines = new InvoiceValueResolver().GetLines(invoice);
        decimal total = 0m;
        foreach (var line in lines)
        {
            var qty = GetDecimal(line, "Quantity", "Qty") ?? 0m;
            var price = GetDecimal(line, "UnitPrice", "Price") ?? 0m;
            total += qty * price;
        }
        return total;
    }

    private static string FormatDate(DateTimeOffset? dto)
        => dto.HasValue ? dto.Value.ToLocalTime().ToString("yyyy-MM-dd") : string.Empty;

    private static string FormatMoney(decimal? value)
        => value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty;

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
            if (v is DateTime dt) return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
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
            if (v is decimal d) return d;
            if (v is double db) return (decimal)db;
            if (v is float f) return (decimal)f;
            if (decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
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

