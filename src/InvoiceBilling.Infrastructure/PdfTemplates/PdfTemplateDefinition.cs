using System.Text.Json.Serialization;

namespace InvoiceBilling.Infrastructure.PdfTemplates;

/// <summary>
/// JSON schema for the template designer UI.
/// All coordinates are in points (pt). A4 page = 595 × 842 pt.
/// </summary>
public sealed class PdfTemplateDefinition
{
    [JsonPropertyName("page")]
    public PdfPageSpec Page { get; set; } = new();

    [JsonPropertyName("fields")]
    public List<PdfTemplateField> Fields { get; set; } = new();

    /// <summary>
    /// Horizontal and vertical rule lines added in the designer.
    /// Each line is drawn as a straight stroke on the page.
    /// </summary>
    [JsonPropertyName("lines")]
    public List<PdfLineElement> Lines { get; set; } = new();

    [JsonPropertyName("linesTable")]
    public PdfLinesTableSpec? LinesTable { get; set; }
}

/// <summary>
/// A single horizontal or vertical rule line drawn on the PDF page.
/// </summary>
public sealed class PdfLineElement
{
    /// <summary>UI element id – ignored by the renderer.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>"H" for horizontal, "V" for vertical.</summary>
    [JsonPropertyName("orientation")]
    public string Orientation { get; set; } = "H";

    /// <summary>X coordinate of the line start, in points.</summary>
    [JsonPropertyName("x")]
    public double X { get; set; }

    /// <summary>Y coordinate of the line start, in points.</summary>
    [JsonPropertyName("y")]
    public double Y { get; set; }

    /// <summary>Total length of the line, in points.</summary>
    [JsonPropertyName("length")]
    public double Length { get; set; }

    /// <summary>Stroke width in points. 1 = hairline.</summary>
    [JsonPropertyName("thickness")]
    public double Thickness { get; set; } = 1;

    /// <summary>Hex stroke colour, e.g. "#000000". Defaults to black.</summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }
}

public sealed class PdfPageSpec
{
    /// <summary>Page width in points (A4 default: 595).</summary>
    [JsonPropertyName("width")]
    public double Width { get; set; } = 595;

    /// <summary>Page height in points (A4 default: 842).</summary>
    [JsonPropertyName("height")]
    public double Height { get; set; } = 842;

    [JsonPropertyName("margin")]
    public double Margin { get; set; } = 40;
}

public sealed class PdfTemplateField
{
    /// <summary>
    /// Value key. Examples: invoiceNumber, issueDate, dueDate, customerName,
    /// subtotal, taxTotal, total, paidTotal, balanceDue, status.
    /// Use "text:" prefix for literals e.g. "text:INVOICE".
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("w")]
    public double W { get; set; }

    [JsonPropertyName("h")]
    public double H { get; set; }

    [JsonPropertyName("font")]
    public PdfFontSpec Font { get; set; } = new();

    /// <summary>Hex color e.g. #333333. Defaults to black.</summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    /// <summary>Text alignment: Left, Center, Right. Defaults to Left.</summary>
    [JsonPropertyName("align")]
    public string? Align { get; set; }
}

public sealed class PdfFontSpec
{
    [JsonPropertyName("family")]
    public string Family { get; set; } = "Arial";

    [JsonPropertyName("size")]
    public double Size { get; set; } = 10;

    [JsonPropertyName("bold")]
    public bool Bold { get; set; }

    [JsonPropertyName("italic")]
    public bool Italic { get; set; }
}

public sealed class PdfLinesTableSpec
{
    [JsonPropertyName("x")]
    public double X { get; set; } = 40;

    [JsonPropertyName("y")]
    public double Y { get; set; } = 200;

    [JsonPropertyName("w")]
    public double W { get; set; } = 515;

    [JsonPropertyName("h")]
    public double H { get; set; } = 400;

    /// <summary>Height of each row in points.</summary>
    [JsonPropertyName("rowHeight")]
    public double RowHeight { get; set; } = 16;

    [JsonPropertyName("headerFont")]
    public PdfFontSpec HeaderFont { get; set; } = new() { Bold = true };

    [JsonPropertyName("rowFont")]
    public PdfFontSpec RowFont { get; set; } = new();

    [JsonPropertyName("columns")]
    public List<PdfTableColumnSpec> Columns { get; set; } = new();
}

public sealed class PdfTableColumnSpec
{
    /// <summary>
    /// Data key. Built-in short keys: Description, Qty, Rate, Amount.
    /// Also accepts: line.description, line.quantity, line.unitPrice, line.total.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Column header label. Falls back to Key if not set.</summary>
    [JsonPropertyName("header")]
    public string? Header { get; set; }

    /// <summary>Column width in points.</summary>
    [JsonPropertyName("w")]
    public double W { get; set; }

    /// <summary>Text alignment: Left, Center, Right.</summary>
    [JsonPropertyName("align")]
    public string? Align { get; set; }
}
