namespace InvoiceBilling.Infrastructure.PdfTemplates;

/// <summary>
/// Configuration for PDF template storage.
/// Default is a local JSON file in App_Data so a small business can self-host cheaply.
/// </summary>
public sealed class PdfTemplatesOptions
{
    public const string SectionName = "PdfTemplates";

    /// <summary>
    /// Relative or absolute path to the template json file.
    /// If relative, it is resolved from the app ContentRootPath.
    /// </summary>
    public string StoragePath { get; set; } = "App_Data/pdf-template.active.json";
}

