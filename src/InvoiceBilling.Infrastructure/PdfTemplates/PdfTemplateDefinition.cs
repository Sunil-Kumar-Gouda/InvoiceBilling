using System.Text.Json.Serialization;

namespace InvoiceBilling.Infrastructure.PdfTemplates;

/// <summary>
/// JSON schema for your template designer UI.
/// Keep this stable: the UI can save/load it, and the worker can render from it.
/// </summary>
public sealed class PdfTemplateDefinition
{
    [JsonPropertyName("page")]
    public PdfPageSpec Page { get; set; } = new();

    /// <summary>
    /// Free-positioned text fields.
    /// Keys map to invoice values (see <see cref="InvoiceValueResolver"/>).
    /// </summary>
    [JsonPropertyName("fields")]
    public List<PdfTemplateField> Fields { get; set; } = new();

    [JsonPropertyName("linesTable")]
    public PdfLinesTableSpec? LinesTable { get; set; }
}

public sealed class PdfPageSpec
{
    /// <summary>Page width in mm (default A4: 210mm).</summary>
    [JsonPropertyName("widthMm")]
    public double WidthMm { get; set; } = 210;

    /// <summary>Page height in mm (default A4: 297mm).</summary>
    [JsonPropertyName("heightMm")]
    public double HeightMm { get; set; } = 297;

    [JsonPropertyName("marginMm")]
    public double MarginMm { get; set; } = 10;
}

public sealed class PdfTemplateField
{
    /// <summary>
    /// Value key. Example: invoiceNumber, issueDate, dueDate, customerName, total, balanceDue.
    /// You can also use literal text by prefixing with "text:" (e.g. "text:TAX INVOICE").
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Position X in millimeters.</summary>
    [JsonPropertyName("xMm")]
    public double Xmm { get; set; }

    /// <summary>Position Y in millimeters.</summary>
    [JsonPropertyName("yMm")]
    public double Ymm { get; set; }

    [JsonPropertyName("font")]
    public PdfFontSpec Font { get; set; } = new();

    /// <summary>Optional color in hex (e.g. #111111). If empty, defaults to black.</summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }
}

public sealed class PdfFontSpec
{
    [JsonPropertyName("family")]
    public string Family { get; set; } = "Segoe UI";

    [JsonPropertyName("size")]
    public double Size { get; set; } = 10;

    [JsonPropertyName("bold")]
    public bool Bold { get; set; }

    [JsonPropertyName("italic")]
    public bool Italic { get; set; }
}

public sealed class PdfLinesTableSpec
{
    [JsonPropertyName("xMm")]
    public double Xmm { get; set; } = 10;

    [JsonPropertyName("yMm")]
    public double Ymm { get; set; } = 70;

    [JsonPropertyName("rowHeightMm")]
    public double RowHeightMm { get; set; } = 6;

    [JsonPropertyName("header")]
    public PdfTableHeaderSpec Header { get; set; } = new();

    [JsonPropertyName("columns")]
    public List<PdfTableColumnSpec> Columns { get; set; } = new();
}

public sealed class PdfTableHeaderSpec
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("font")]
    public PdfFontSpec Font { get; set; } = new() { Bold = true };
}

public sealed class PdfTableColumnSpec
{
    /// <summary>Example: line.description, line.quantity, line.unitPrice, line.total.</summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("header")]
    public string Header { get; set; } = string.Empty;

    [JsonPropertyName("widthMm")]
    public double WidthMm { get; set; }
}

