using InvoiceBilling.Infrastructure.Persistence;
using InvoiceBilling.Infrastructure.PdfTemplates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InvoiceBilling.Api.Controllers;

[ApiController]
[Route("api/pdf-templates")]
//[Authorize(Roles = "Admin,TemplateDesigner")]
//[Authorize(Roles = "Admin")]
public sealed class PdfTemplatesController : ControllerBase
{
    private readonly IPdfTemplateStore _store;
    private readonly InvoiceBillingDbContext _db;
    private readonly IInvoicePdfTemplateRenderer _renderer;

    public PdfTemplatesController(IPdfTemplateStore store, InvoiceBillingDbContext db, IInvoicePdfTemplateRenderer renderer)
    {
        _store = store;
        _db = db;
        _renderer = renderer;
    }

    /// <summary>
    /// Returns the active template JSON (exactly as saved by the UI).
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var raw = await _store.GetActiveTemplateJsonAsync(ct);
        if (raw is null) return NotFound();
        return Content(raw, "application/json");
    }

    /// <summary>
    /// Upserts the active template JSON.
    /// </summary>
    [HttpPut("active")]
    public async Task<IActionResult> PutActive([FromBody] JsonElement template, CancellationToken ct)
    {
        await _store.SaveActiveTemplateJsonAsync(template.GetRawText(), ct);
        return NoContent();
    }

    /// <summary>
    /// Deletes the active template.
    /// </summary>
    [HttpDelete("active")]
    public async Task<IActionResult> DeleteActive(CancellationToken ct)
    {
        await _store.DeleteActiveTemplateAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Generates a PDF preview for a given invoice using the provided template JSON.
    /// (Does NOT persist the template.)
    /// </summary>
    [HttpPost("preview/{invoiceId:guid}")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Preview(Guid invoiceId, [FromBody] JsonElement template, CancellationToken ct)
    {
        // Load invoice without relying on compile-time nav property names.
        var invoice = await FindInvoiceAsync(invoiceId, ct);

        if (invoice is null) return NotFound();

        await TryLoadReferenceAsync(invoice, "CustomerId", ct);
        await TryLoadCollectionAsync(invoice, "Lines", ct);
        await TryLoadCollectionAsync(invoice, "InvoiceLines", ct);
        await TryLoadCollectionAsync(invoice, "Items", ct);

        var def = InvoicePdfTemplateRenderer.ParseTemplate(template);
        var pdf = _renderer.Render(invoice, def);
        return File(pdf, "application/pdf", $"invoice-{invoiceId}-preview.pdf");
    }

    private async Task<object?> FindInvoiceAsync(Guid invoiceId, CancellationToken ct)
    {
        var invoiceClrType = _db.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .FirstOrDefault(t => string.Equals(t.Name, "Invoice", StringComparison.Ordinal));

        if (invoiceClrType is null)
        {
            return null;
        }

        try
        {
            return await _db.FindAsync(invoiceClrType, new object?[] { invoiceId }, ct);
        }
        catch
        {
            return null;
        }
    }

    private async Task TryLoadReferenceAsync(object entity, string referenceName, CancellationToken ct)
    {
        try
        {
            var entry = _db.Entry(entity);
            var reference = entry.Reference(referenceName);
            if (!reference.IsLoaded)
                await reference.LoadAsync(ct);
        }
        catch(Exception ex)
        {
            // Ignore if nav doesn't exist
        }
    }

    private async Task TryLoadCollectionAsync(object entity, string collectionName, CancellationToken ct)
    {
        try
        {
            var entry = _db.Entry(entity);
            var collection = entry.Collection(collectionName);
            if (!collection.IsLoaded)
                await collection.LoadAsync(ct);
        }
        catch(Exception ex)
        {
            // Ignore if nav doesn't exist
        }
    }
}

