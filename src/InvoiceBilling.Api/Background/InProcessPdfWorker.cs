using System.Text.Json;
using InvoiceBilling.Application.Common.PdfTemplates;
using InvoiceBilling.Domain.Exceptions;
using InvoiceBilling.Infrastructure.Persistence;
using InvoiceBilling.Infrastructure.PdfTemplates;
using InvoiceBilling.Infrastructure.Standalone;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InvoiceBilling.Api.Background;

/// <summary>
/// Standalone replacement for <see cref="InvoicePdfWorker"/>.
/// Reads invoice IDs from an in-process <see cref="InProcessPdfJobChannel"/>,
/// renders PDFs using the same template engine, and writes them to the local
/// filesystem instead of S3.
/// </summary>
/// <remarks>
/// <para>
/// Design decisions aligned with the existing cloud worker:
/// <list type="bullet">
///   <item>Idempotent: skips invoices that already have a <c>PdfS3Key</c> attached.</item>
///   <item>Resilient: catches and logs rendering failures without crashing the host.</item>
///   <item>Scoped: creates a fresh <c>DbContext</c> per job to avoid stale-entity issues.</item>
/// </list>
/// </para>
/// <para>
/// The key stored in <see cref="Domain.Entities.Invoice.PdfS3Key"/> uses the same
/// <c>invoices/{id}.pdf</c> format as cloud mode. This means switching from standalone
/// to cloud only requires copying files into the S3 bucket — no database migration needed.
/// </para>
/// </remarks>
public sealed class InProcessPdfWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InProcessPdfJobChannel _channel;
    private readonly StandaloneOptions _options;
    private readonly ILogger<InProcessPdfWorker> _logger;

    public InProcessPdfWorker(
        IServiceScopeFactory scopeFactory,
        InProcessPdfJobChannel channel,
        IOptions<StandaloneOptions> options,
        ILogger<InProcessPdfWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _channel = channel;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "InProcessPdfWorker started. PdfStoragePath={PdfStoragePath}, MaxConcurrency={MaxConcurrency}.",
            _options.PdfStoragePath,
            _options.MaxConcurrency);

        // Use a SemaphoreSlim to cap concurrent PDF renders.
        // PdfSharp is CPU-bound; unbounded parallelism would starve the API thread pool.
        using var throttle = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);

        try
        {
            await foreach (var invoiceId in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                await throttle.WaitAsync(stoppingToken);

                // Fire-and-forget within the semaphore. Exceptions are caught inside ProcessJobAsync.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessJobAsync(invoiceId, stoppingToken);
                    }
                    finally
                    {
                        throttle.Release();
                    }
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown — host is stopping.
        }

        _logger.LogInformation("InProcessPdfWorker stopped.");
    }

    private async Task ProcessJobAsync(Guid invoiceId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InvoiceBillingDbContext>();

            var invoice = await db.Invoices
                .Include(i => i.Lines)
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

            if (invoice is null)
            {
                _logger.LogWarning("Invoice {InvoiceId} not found. Skipping.", invoiceId);
                return;
            }

            // Idempotency: if PDF was already generated (e.g. duplicate enqueue), skip.
            if (!string.IsNullOrWhiteSpace(invoice.PdfS3Key))
            {
                _logger.LogInformation(
                    "Invoice {InvoiceId} already has PdfS3Key={Key}. Skipping.",
                    invoiceId, invoice.PdfS3Key);
                return;
            }

            // ── Render PDF ──────────────────────────────────────────────

            var templateStore = scope.ServiceProvider.GetRequiredService<IActivePdfTemplateStore>();
            var templateRenderer = scope.ServiceProvider.GetRequiredService<IInvoicePdfTemplateRenderer>();

            var templateJson = await templateStore.GetActiveTemplateJsonAsync(ct);
            if (string.IsNullOrWhiteSpace(templateJson))
            {
                var defaultTemplatePath = Path.Combine(
                    AppContext.BaseDirectory, "App_Data", "pdf-template.default.json");
                templateJson = await File.ReadAllTextAsync(defaultTemplatePath, ct);
            }

            var templateDef = InvoicePdfTemplateRenderer.ParseTemplate(
                JsonDocument.Parse(templateJson).RootElement);

            var pdfBytes = templateRenderer.Render(invoice, templateDef);

            // ── Write to local filesystem ───────────────────────────────

            // Use the same key format as cloud mode for seamless migration.
            var key = $"invoices/{invoiceId}.pdf";

            var localStorage = scope.ServiceProvider.GetRequiredService<LocalFileInvoicePdfStorage>();
            var filePath = localStorage.ResolveFullPath(key);

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllBytesAsync(filePath, pdfBytes, ct);

            // ── Attach to invoice ───────────────────────────────────────

            try
            {
                invoice.AttachPdf(key);
                await db.SaveChangesAsync(ct);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex,
                    "Domain rule prevented attaching PDF for InvoiceId={InvoiceId}.", invoiceId);
            }

            _logger.LogInformation(
                "Processed invoice PDF {InvoiceId} -> {FilePath}.", invoiceId, filePath);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host shutdown — let it propagate silently.
        }
        catch (Exception ex)
        {
            // Log and swallow so one bad invoice does not kill the worker loop.
            _logger.LogError(ex,
                "Failed to generate PDF for InvoiceId={InvoiceId}. " +
                "The job will not be retried automatically; re-issue the invoice to regenerate.",
                invoiceId);
        }
    }
}
