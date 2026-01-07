namespace InvoiceBilling.Application.Common.Jobs;

public interface IInvoicePdfJobEnqueuer
{
    Task EnqueueInvoicePdfJobAsync(Guid invoiceId, CancellationToken ct);
}
