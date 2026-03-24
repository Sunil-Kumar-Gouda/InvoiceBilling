using System.Threading.Channels;

namespace InvoiceBilling.Infrastructure.Standalone;

/// <summary>
/// Shared, bounded in-process channel that replaces SQS in standalone mode.
/// Registered as a singleton so the enqueuer and the background worker share
/// the same instance.
/// </summary>
/// <remarks>
/// <para>
/// The channel is bounded to prevent unbounded memory growth if the worker
/// falls behind. <see cref="BoundedChannelFullMode.Wait"/> applies back-pressure
/// to the API thread, which is the safest default for a single-machine deployment.
/// </para>
/// <para>
/// Trade-off: messages are lost on process restart. This is acceptable for a
/// small-shop deployment where invoices can be re-issued. For durability
/// guarantees, use Cloud mode with SQS.
/// </para>
/// </remarks>
public sealed class InProcessPdfJobChannel
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(capacity: 256) { FullMode = BoundedChannelFullMode.Wait });

    public ChannelReader<Guid> Reader => _channel.Reader;
    public ChannelWriter<Guid> Writer => _channel.Writer;
}
