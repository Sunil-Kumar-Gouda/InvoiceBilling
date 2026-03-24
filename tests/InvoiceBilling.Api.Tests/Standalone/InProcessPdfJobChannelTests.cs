using InvoiceBilling.Infrastructure.Standalone;

namespace InvoiceBilling.Api.Tests.Standalone;

public sealed class InProcessPdfJobChannelTests
{
    [Fact]
    public async Task WriteAsync_and_ReadAsync_round_trip_a_single_invoiceId()
    {
        var channel = new InProcessPdfJobChannel();
        var expected = Guid.NewGuid();

        await channel.Writer.WriteAsync(expected);

        var actual = await channel.Reader.ReadAsync();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Channel_preserves_FIFO_ordering()
    {
        var channel = new InProcessPdfJobChannel();

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        await channel.Writer.WriteAsync(first);
        await channel.Writer.WriteAsync(second);
        await channel.Writer.WriteAsync(third);

        Assert.Equal(first, await channel.Reader.ReadAsync());
        Assert.Equal(second, await channel.Reader.ReadAsync());
        Assert.Equal(third, await channel.Reader.ReadAsync());
    }

    [Fact]
    public async Task Reader_Count_reflects_pending_items()
    {
        var channel = new InProcessPdfJobChannel();

        Assert.Equal(0, channel.Reader.Count);

        await channel.Writer.WriteAsync(Guid.NewGuid());
        await channel.Writer.WriteAsync(Guid.NewGuid());

        Assert.Equal(2, channel.Reader.Count);

        _ = await channel.Reader.ReadAsync();

        Assert.Equal(1, channel.Reader.Count);
    }

    [Fact]
    public async Task ReadAsync_blocks_until_item_is_available()
    {
        var channel = new InProcessPdfJobChannel();
        var readCompleted = false;
        var expected = Guid.NewGuid();

        var readTask = Task.Run(async () =>
        {
            var result = await channel.Reader.ReadAsync();
            readCompleted = true;
            return result;
        });

        // Give the reader a moment to block
        await Task.Delay(50);
        Assert.False(readCompleted, "ReadAsync should block when channel is empty.");

        // Unblock by writing
        await channel.Writer.WriteAsync(expected);

        var actual = await readTask;

        Assert.True(readCompleted);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Channel_is_bounded_at_256()
    {
        var channel = new InProcessPdfJobChannel();

        // Fill the channel to capacity
        for (var i = 0; i < 256; i++)
            await channel.Writer.WriteAsync(Guid.NewGuid());

        // The 257th write should not complete within a short timeout
        // because the channel is full and FullMode is Wait.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await channel.Writer.WriteAsync(Guid.NewGuid(), cts.Token));
    }
}
