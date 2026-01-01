using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using InvoiceBilling.Infrastructure.Cloud;
using InvoiceBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace InvoiceBilling.Api.Background;

public sealed class InvoicePdfWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAmazonSQS _sqs;
    private readonly IAmazonS3 _s3;
    private readonly AwsOptions _aws;
    private readonly ILogger<InvoicePdfWorker> _logger;

    public InvoicePdfWorker(
        IServiceScopeFactory scopeFactory,
        IAmazonSQS sqs,
        IAmazonS3 s3,
        IOptions<AwsOptions> awsOptions,
        ILogger<InvoicePdfWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _sqs = sqs;
        _s3 = s3;
        _aws = awsOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Hard guard: fail fast with a clear error if config is missing
        if (string.IsNullOrWhiteSpace(_aws?.ServiceUrl) ||
            string.IsNullOrWhiteSpace(_aws?.Sqs?.QueueName) ||
            string.IsNullOrWhiteSpace(_aws?.S3?.BucketName))
        {
            throw new InvalidOperationException(
                "AWS settings missing. Ensure configuration has Aws:ServiceUrl, Aws:Sqs:QueueName, Aws:S3:BucketName.");
        }

        _logger.LogInformation(
            "InvoicePdfWorker started. ServiceUrl={ServiceUrl}, QueueName={QueueName}, Bucket={Bucket}",
            _aws.ServiceUrl, _aws.Sqs.QueueName, _aws.S3.BucketName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1) Get queue URL safely
                var queueResp = await _sqs.GetQueueUrlAsync(_aws.Sqs.QueueName, stoppingToken);
                var queueUrl = queueResp?.QueueUrl;

                if (string.IsNullOrWhiteSpace(queueUrl))
                {
                    _logger.LogWarning("SQS queue URL not found for queue {QueueName}", _aws.Sqs.QueueName);
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                // 2) Receive messages safely
                var resp = await _sqs.ReceiveMessageAsync(new Amazon.SQS.Model.ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 5,
                    WaitTimeSeconds = 10
                }, stoppingToken);

                var messages = resp?.Messages;
                if (messages == null || messages.Count == 0)
                    continue;

                foreach (var msg in messages)
                {
                    await ProcessMessage(queueUrl, msg, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop error.");
                await Task.Delay(1500, stoppingToken);
            }
        }
    }

    private static bool TryGetInvoiceId(string body, out Guid invoiceId)
    {
        invoiceId = Guid.Empty;

        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("invoiceId", out var prop))
            return false;

        // Supports either GUID string or GUID JSON token
        if (prop.ValueKind == JsonValueKind.String)
            return Guid.TryParse(prop.GetString(), out invoiceId);

        if (prop.ValueKind == JsonValueKind.Undefined || prop.ValueKind == JsonValueKind.Null)
            return false;

        // If it was serialized as a GUID token-like string, still try ToString()
        return Guid.TryParse(prop.ToString(), out invoiceId);
    }

    private async Task SafeDelete(string queueUrl, Message msg, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(queueUrl))
        {
            _logger.LogWarning("Cannot delete message: queueUrl is empty. MessageId={MessageId}", msg?.MessageId);
            return;
        }

        if (string.IsNullOrWhiteSpace(msg?.ReceiptHandle))
        {
            _logger.LogWarning("Cannot delete message: ReceiptHandle missing. MessageId={MessageId}", msg?.MessageId);
            return;
        }

        await _sqs.DeleteMessageAsync(queueUrl, msg.ReceiptHandle, ct);
    }

    private async Task ProcessMessage(string queueUrl, Message msg, CancellationToken ct)
    {
        if (msg is null)
        {
            _logger.LogWarning("Received null SQS message instance.");
            return;
        }

        if (string.IsNullOrWhiteSpace(msg.Body))
        {
            _logger.LogWarning("Received SQS message with empty Body. Deleting. MessageId={MessageId}", msg.MessageId);
            await SafeDelete(queueUrl, msg, ct);
            return;
        }

        Guid invoiceId;
        try
        {
            if (!TryGetInvoiceId(msg.Body, out invoiceId))
            {
                _logger.LogWarning("Invalid message schema (missing/invalid invoiceId). Deleting. Body={Body}", msg.Body);
                await SafeDelete(queueUrl, msg, ct);
                return;
            }
        }
        catch (Exception ex)
        {
            // JSON parse errors are poison messages; delete them
            _logger.LogWarning(ex, "Invalid JSON message. Deleting. Body={Body}", msg.Body);
            await SafeDelete(queueUrl, msg, ct);
            return;
        }

        try
        {
            // Update DB first (and enforce idempotency)
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InvoiceBillingDbContext>();

            var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
            if (invoice is null)
            {
                _logger.LogWarning("Invoice {InvoiceId} not found. Deleting message to avoid retries.", invoiceId);
                await SafeDelete(queueUrl, msg, ct);
                return;
            }

            // Idempotency: if already generated, just delete message
            if (!string.IsNullOrWhiteSpace(invoice.PdfS3Key))
            {
                _logger.LogInformation("Invoice {InvoiceId} already has PdfS3Key={Key}. Deleting message.", invoiceId, invoice.PdfS3Key);
                await SafeDelete(queueUrl, msg, ct);
                return;
            }

            var bucket = _aws.S3?.BucketName;
            if (string.IsNullOrWhiteSpace(bucket))
                throw new InvalidOperationException("Aws:S3:BucketName is missing.");

            var content = $"Invoice PDF placeholder\nInvoiceId: {invoiceId}\nGeneratedAtUtc: {DateTime.UtcNow:o}\n";
            var bytes = Encoding.UTF8.GetBytes(content);

            var key = $"invoices/{invoiceId}.txt";

            await using var ms = new MemoryStream(bytes);
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = ms,
                ContentType = "text/plain"
            }, ct);

            invoice.PdfS3Key = key;
            await db.SaveChangesAsync(ct);

            await SafeDelete(queueUrl, msg, ct);
            _logger.LogInformation("Processed invoice job {InvoiceId} -> {Key}", invoiceId, key);
        }
        catch (Exception ex)
        {
            // For transient errors (S3/SQS/DB), keep message so it can retry
            _logger.LogError(ex, "Failed processing invoice message. Will retry. MessageId={MessageId} Body={Body}", msg.MessageId, msg.Body);
        }
    }

}
