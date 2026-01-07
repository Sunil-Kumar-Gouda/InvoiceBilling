using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using InvoiceBilling.Application.Common.Jobs;
using Microsoft.Extensions.Options;

namespace InvoiceBilling.Infrastructure.Cloud;

public sealed class SqsInvoicePdfJobEnqueuer : IInvoicePdfJobEnqueuer
{
    private readonly IAmazonSQS _sqs;
    private readonly AwsOptions _aws;
    private string? _queueUrl;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SqsInvoicePdfJobEnqueuer(IAmazonSQS sqs, IOptions<AwsOptions> awsOptions)
    {
        _sqs = sqs;
        _aws = awsOptions.Value;
    }

    public async Task EnqueueInvoicePdfJobAsync(Guid invoiceId, CancellationToken ct)
    {
        var queueName = _aws.Sqs?.QueueName;
        if (string.IsNullOrWhiteSpace(queueName))
            throw new InvalidOperationException("Aws:Sqs:QueueName is missing.");

        _queueUrl ??= (await _sqs.GetQueueUrlAsync(queueName, ct)).QueueUrl;

        var payload = JsonSerializer.Serialize(new { invoiceId }, JsonOptions);

        await _sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = payload
        }, ct);
    }
}
