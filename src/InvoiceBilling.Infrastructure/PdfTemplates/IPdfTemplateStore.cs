using System.Text.Json;

namespace InvoiceBilling.Infrastructure.PdfTemplates;

/// <summary>
/// Minimal persistence for a single "active" PDF template.
/// Day-15 uses a file-based store to keep migration risk low.
/// Replace with a DB-backed store later without changing callers.
/// </summary>
public interface IPdfTemplateStore
{
    /// <summary>Gets the active template as raw JSON, or null if none exists.</summary>
    Task<string?> GetActiveTemplateJsonAsync(CancellationToken ct);

    /// <summary>Upserts the active template from JSON.</summary>
    Task SaveActiveTemplateJsonAsync(string json, CancellationToken ct);

    /// <summary>Deletes the active template (revert to built-in layout).</summary>
    Task DeleteActiveTemplateAsync(CancellationToken ct);
}

