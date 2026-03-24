using InvoiceBilling.Infrastructure.Standalone;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvoiceBilling.Api.Tests.Standalone;

public sealed class InProcessPdfJobEnqueuerTests
{
    [Fact]
    public async Task Enqueue_writes_invoiceId_to_channel_when_worker_enabled()
    {
        var channel = new InProcessPdfJobChannel();
        var enqueuer = CreateEnqueuer(channel, workerEnabled: true);

        var invoiceId = Guid.NewGuid();
        await enqueuer.EnqueueInvoicePdfJobAsync(invoiceId, CancellationToken.None);

        Assert.Equal(1, channel.Reader.Count);

        var dequeued = await channel.Reader.ReadAsync();
        Assert.Equal(invoiceId, dequeued);
    }

    [Fact]
    public async Task Enqueue_skips_writing_when_worker_disabled()
    {
        var channel = new InProcessPdfJobChannel();
        var enqueuer = CreateEnqueuer(channel, workerEnabled: false);

        var invoiceId = Guid.NewGuid();
        await enqueuer.EnqueueInvoicePdfJobAsync(invoiceId, CancellationToken.None);

        Assert.Equal(0, channel.Reader.Count);
    }

    [Fact]
    public async Task Enqueue_does_not_throw_when_worker_disabled()
    {
        var channel = new InProcessPdfJobChannel();
        var enqueuer = CreateEnqueuer(channel, workerEnabled: false);

        // Should complete without exception
        await enqueuer.EnqueueInvoicePdfJobAsync(Guid.NewGuid(), CancellationToken.None);
        await enqueuer.EnqueueInvoicePdfJobAsync(Guid.NewGuid(), CancellationToken.None);
        await enqueuer.EnqueueInvoicePdfJobAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(0, channel.Reader.Count);
    }

    [Fact]
    public async Task Enqueue_multiple_items_preserves_order()
    {
        var channel = new InProcessPdfJobChannel();
        var enqueuer = CreateEnqueuer(channel, workerEnabled: true);

        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        foreach (var id in ids)
            await enqueuer.EnqueueInvoicePdfJobAsync(id, CancellationToken.None);

        Assert.Equal(3, channel.Reader.Count);

        for (var i = 0; i < ids.Length; i++)
        {
            var dequeued = await channel.Reader.ReadAsync();
            Assert.Equal(ids[i], dequeued);
        }
    }

    [Fact]
    public async Task Enqueue_respects_cancellation_token()
    {
        var channel = new InProcessPdfJobChannel();
        var enqueuer = CreateEnqueuer(channel, workerEnabled: true);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        await enqueuer.EnqueueInvoicePdfJobAsync(Guid.NewGuid(), cts.Token));

        Assert.True(ex.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Enqueue_skipped_when_worker_config_key_is_absent()
    {
        // If the config key is missing entirely, GetValue<bool> defaults to false.
        // The enqueuer should treat missing config as "worker disabled".
        var channel = new InProcessPdfJobChannel();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var enqueuer = new InProcessPdfJobEnqueuer(
            channel,
            config,
            NullLogger<InProcessPdfJobEnqueuer>.Instance);

        await enqueuer.EnqueueInvoicePdfJobAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(0, channel.Reader.Count);
    }

    private static InProcessPdfJobEnqueuer CreateEnqueuer(
        InProcessPdfJobChannel channel,
        bool workerEnabled)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundWorkers:InvoicePdfWorker:Enabled"] = workerEnabled.ToString()
            })
            .Build();

        return new InProcessPdfJobEnqueuer(
            channel,
            config,
            NullLogger<InProcessPdfJobEnqueuer>.Instance);
    }
}
