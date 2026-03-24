namespace InvoiceBilling.Infrastructure.Standalone;

/// <summary>
/// Configuration for standalone infrastructure mode.
/// Bound from <c>Infrastructure:Standalone</c> in appsettings.
/// </summary>
public sealed class StandaloneOptions
{
    public const string SectionName = "Infrastructure:Standalone";

    /// <summary>
    /// Relative or absolute path to the directory where generated invoice PDFs are stored.
    /// When relative, resolved from the application's <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.ContentRootPath"/>.
    /// </summary>
    /// <remarks>
    /// The directory is created automatically on startup if it does not exist.
    /// Defaults to <c>App_Data/invoices</c> alongside the SQLite database, keeping
    /// all persistent state co-located for simple backups.
    /// </remarks>
    public string PdfStoragePath { get; set; } = "App_Data/invoices";

    /// <summary>
    /// Maximum number of concurrent PDF rendering tasks. Defaults to 2.
    /// </summary>
    public int MaxConcurrency { get; set; } = 2;
}
