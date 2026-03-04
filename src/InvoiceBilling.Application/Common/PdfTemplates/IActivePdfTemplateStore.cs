namespace InvoiceBilling.Application.Common.PdfTemplates;

/// <summary>
/// Application-layer abstraction for reading and writing the active PDF template.
/// Defined here so Application handlers remain independent of Infrastructure.
/// Implemented by InvoiceBilling.Infrastructure (FilePdfTemplateStore).
/// </summary>
public interface IActivePdfTemplateStore
{
    Task<string?> GetActiveTemplateJsonAsync(CancellationToken ct);
    Task SaveActiveTemplateJsonAsync(string json, CancellationToken ct);
    Task DeleteActiveTemplateAsync(CancellationToken ct);
}
