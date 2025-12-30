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

    private async Task ProcessMessage(string queueUrl, Message msg, CancellationToken ct)
    {
        try
        {
            var doc = JsonDocument.Parse(msg.Body);
            var invoiceId = doc.RootElement.GetProperty("invoiceId").GetGuid();

            var content = $"Invoice PDF placeholder\nInvoiceId: {invoiceId}\nGeneratedAtUtc: {DateTime.UtcNow:o}\n";
            var bytes = Encoding.UTF8.GetBytes(content);

            var key = $"invoices/{invoiceId}.txt";

            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _aws.S3.BucketName,
                Key = key,
                InputStream = new MemoryStream(bytes),
                ContentType = "text/plain"
            }, ct);

            // Update DB with S3 key
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InvoiceBillingDbContext>();

            var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
            if (invoice != null)
            {
                invoice.PdfS3Key = key;
                await db.SaveChangesAsync(ct);
            }

            await _sqs.DeleteMessageAsync(queueUrl, msg.ReceiptHandle, ct);
            _logger.LogInformation("Processed invoice job {InvoiceId} -> {Key}", invoiceId, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing message: {Body}", msg.Body);
        }
    }
}
