using InvoiceBilling.Application.Common.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InvoiceBilling.Infrastructure.Standalone;

/// <summary>
/// Standalone replacement for <see cref="Cloud.SqsInvoicePdfJobEnqueuer"/>.
/// Writes invoice IDs into an in-process <see cref="InProcessPdfJobChannel"/>
/// instead of sending them to SQS.
/// </summary>
/// <remarks>
/// Unlike SQS (which is external storage with virtually unlimited capacity),
/// the in-process channel is bounded. If the background worker is disabled
/// but the enqueuer is still active, the channel will eventually fill up
/// and <see cref="System.Threading.Channels.ChannelWriter{T}.WriteAsync"/>
/// will block the API thread indefinitely. To prevent this, the enqueuer
/// checks <c>BackgroundWorkers:InvoicePdfWorker:Enabled</c> at construction
/// time and silently drops jobs when the worker is not running.
/// </remarks>
public sealed class InProcessPdfJobEnqueuer : IInvoicePdfJobEnqueuer
{
    private readonly InProcessPdfJobChannel _channel;
    private readonly bool _workerEnabled;
    private readonly ILogger<InProcessPdfJobEnqueuer> _logger;

    public InProcessPdfJobEnqueuer(
        InProcessPdfJobChannel channel,
        IConfiguration configuration,
        ILogger<InProcessPdfJobEnqueuer> logger)
    {
        _channel = channel;
        _workerEnabled = configuration.GetValue<bool>("BackgroundWorkers:InvoicePdfWorker:Enabled");
        _logger = logger;
    }

    public async Task EnqueueInvoicePdfJobAsync(Guid invoiceId, CancellationToken ct)
    {
        if (!_workerEnabled)
        {
            _logger.LogWarning(
                "PDF worker is disabled. Skipping enqueue for InvoiceId={InvoiceId}.",
                invoiceId);
            return;
        }

        await _channel.Writer.WriteAsync(invoiceId, ct);

        _logger.LogInformation(
            "Enqueued in-process PDF job for InvoiceId={InvoiceId}. " +
            "Approximate pending items: {PendingCount}.",
            invoiceId,
            // Reader.Count is a best-effort snapshot; fine for diagnostics.
            _channel.Reader.Count);
    }
}
