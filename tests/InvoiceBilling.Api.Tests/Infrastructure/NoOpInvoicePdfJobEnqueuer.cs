using InvoiceBilling.Application.Common.Jobs;

namespace InvoiceBilling.Api.Tests.Infrastructure;

internal sealed class NoOpInvoicePdfJobEnqueuer : IInvoicePdfJobEnqueuer
{
    public Task EnqueueInvoicePdfJobAsync(Guid invoiceId, CancellationToken ct) => Task.CompletedTask;
}
