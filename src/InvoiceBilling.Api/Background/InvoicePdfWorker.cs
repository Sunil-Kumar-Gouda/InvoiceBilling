using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using InvoiceBilling.Domain.Exceptions;
using InvoiceBilling.Infrastructure.Cloud;
using InvoiceBilling.Infrastructure.Persistence;
using InvoiceBilling.Api.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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

    // Loop / backoff tuning
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

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

        var bucket = _aws.S3.BucketName;
        var queueName = _aws.Sqs.QueueName;

        _logger.LogInformation(
            "InvoicePdfWorker started. ServiceUrl={ServiceUrl}, QueueName={QueueName}, Bucket={Bucket}",
            _aws.ServiceUrl, queueName, bucket);

        // Cache queueUrl once it resolves successfully; reset to null when the queue is missing.
        string? queueUrl = null;
        var bucketReady = false;

        // One backoff for infra readiness (SQS/S3) and transient failures.
        var backoff = new ExponentialBackoff(MinBackoff, MaxBackoff);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 0) Ensure S3 bucket is ready (LocalStack often needs a moment after container start)
                if (!bucketReady)
                {
                    bucketReady = await TryEnsureBucketReadyAsync(bucket, stoppingToken);
                    if (!bucketReady)
                    {
                        await DelayWithBackoffAsync(backoff, "S3 bucket not ready", stoppingToken);
                        continue;
                    }
                }

                // Resolve queue URL (cached)
                queueUrl ??= await TryResolveQueueUrlAsync(queueName, stoppingToken);
                if (string.IsNullOrWhiteSpace(queueUrl))
                {
                    queueUrl = null;
                    await DelayWithBackoffAsync(backoff, "SQS queue URL not ready", stoppingToken);
                    continue;
                }

                // Long-poll for messages
                var resp = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 5,
                    WaitTimeSeconds = 10,
                    // Keep a reasonable visibility window for processing
                    VisibilityTimeout = 30
                }, stoppingToken);

                var messages = resp?.Messages;
                if (messages == null || messages.Count == 0)
                {
                    // Infra is reachable and stable.
                    backoff.Reset();
                    continue;
                }

                foreach (var msg in messages)
                    await ProcessMessage(queueUrl, msg, stoppingToken);

                // Completed a batch without transient failures.
                backoff.Reset();
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
                break;
            }
            catch (TransientWorkerException ex) when (ex.InnerException is AmazonS3Exception s3Ex && IsBucketNotReady(s3Ex))
            {
                // Bucket may not exist yet / LocalStack not ready.
                bucketReady = false;
                await DelayWithBackoffAsync(backoff, ex.Message, stoppingToken, ex.InnerException);
            }
            catch (TransientWorkerException ex)
            {
                await DelayWithBackoffAsync(backoff, ex.Message, stoppingToken, ex.InnerException ?? ex);
            }
            catch (QueueDoesNotExistException ex)
            {
                // LocalStack / AWS may briefly report missing queue during startup or re-creation.
                queueUrl = null;
                await DelayWithBackoffAsync(backoff, "SQS queue does not exist yet", stoppingToken, ex);
            }
            catch (AmazonSQSException ex) when (IsQueueMissing(ex))
            {
                queueUrl = null;
                await DelayWithBackoffAsync(backoff, "SQS queue not found yet", stoppingToken, ex);
            }
            catch (AmazonS3Exception ex) when (IsBucketNotReady(ex))
            {
                // Bucket may not exist yet / LocalStack not ready.
                bucketReady = false;
                await DelayWithBackoffAsync(backoff, "S3 bucket not ready yet", stoppingToken, ex);
            }
            catch (HttpRequestException ex)
            {
                // LocalStack/AWS endpoint not reachable (service down, port not mapped, etc.)
                bucketReady = false;
                queueUrl = null;
                await DelayWithBackoffAsync(backoff, $"AWS endpoint not reachable at {_aws.ServiceUrl}", stoppingToken, ex);
            }
            catch (Exception ex)
            {
                await DelayWithBackoffAsync(backoff, "Worker loop error", stoppingToken, ex);
            }
        }

        _logger.LogInformation("InvoicePdfWorker stopped.");
    }

    private static bool IsQueueMissing(AmazonSQSException ex)
    {
        // AWS SDK commonly uses this error code; LocalStack often follows the same.
        if (string.Equals(ex.ErrorCode, "AWS.SimpleQueueService.NonExistentQueue", StringComparison.OrdinalIgnoreCase))
            return true;

        // Sometimes the service returns 400 with a message indicating it doesn't exist.
        return ex.StatusCode == HttpStatusCode.BadRequest &&
               ex.Message.Contains("NonExistentQueue", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBucketNotReady(AmazonS3Exception ex)
    {
        // Missing bucket or not found
        if (string.Equals(ex.ErrorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase))
            return true;

        if (ex.StatusCode == HttpStatusCode.NotFound)
            return true;

        // LocalStack / transient S3 readiness issues sometimes surface as 503
        if (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
            return true;

        return false;
    }

    private async Task<bool> TryEnsureBucketReadyAsync(string bucketName, CancellationToken ct)
    {
        try
        {
            // HeadBucket is the cheapest readiness check.
            await _s3.HeadBucketAsync(new HeadBucketRequest { BucketName = bucketName }, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (IsBucketNotReady(ex))
        {
            // Not ready yet; caller will back off.
            return false;
        }
        catch (HttpRequestException)
        {
            // LocalStack/AWS edge not reachable yet
            return false;
        }
        catch (TaskCanceledException)
        {
            // Transient timeout
            return false;
        }
    }

    private async Task<string?> TryResolveQueueUrlAsync(string queueName, CancellationToken ct)
    {
        try
        {
            var resp = await _sqs.GetQueueUrlAsync(queueName, ct);
            return resp?.QueueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            return null;
        }
        catch (AmazonSQSException ex) when (IsQueueMissing(ex))
        {
            return null;
        }
        catch (HttpRequestException)
        {
            // LocalStack/AWS edge not reachable yet
            return null;
        }
        catch (TaskCanceledException)
        {
            // Transient timeout
            return null;
        }
    }

    private async Task DelayWithBackoffAsync(
        ExponentialBackoff backoff,
        string reason,
        CancellationToken ct,
        Exception? ex = null)
    {
        var delay = backoff.NextDelayWithJitter();

        if (ex is null)
        {
            _logger.LogWarning("{Reason}. Backing off for {DelayMs}ms (attempt {Attempt}).",
                reason, (int)delay.TotalMilliseconds, backoff.Attempt);
        }
        else
        {
            // Avoid log spam: for connectivity errors (LocalStack down), log without full stack trace.
            var root = ex.GetBaseException();
            if (ex is HttpRequestException || ex is TaskCanceledException || root is SocketException)
            {
                _logger.LogWarning("{Reason}. Backing off for {DelayMs}ms (attempt {Attempt}). Details: {Details}",
                    reason, (int)delay.TotalMilliseconds, backoff.Attempt, root.Message);
            }
            else
            {
                _logger.LogWarning(ex, "{Reason}. Backing off for {DelayMs}ms (attempt {Attempt}).",
                    reason, (int)delay.TotalMilliseconds, backoff.Attempt);
            }
        }

        await Task.Delay(delay, ct);
    }

    private static bool TryGetInvoiceId(JsonDocument doc, out Guid invoiceId)
    {
        invoiceId = Guid.Empty;

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

        using var doc = await ParseOrDeleteAsync(msg, queueUrl, ct);
        if (doc is null) return;

        if (!TryGetInvoiceId(doc, out var invoiceId))
        {
            _logger.LogWarning("Invalid message schema (missing/invalid invoiceId). Deleting. Body={Body}", msg.Body);
            await SafeDelete(queueUrl, msg, ct);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InvoiceBillingDbContext>();

            var invoice = await db.Invoices
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
            if (invoice is null)
            {
                _logger.LogWarning("Invoice {InvoiceId} not found. Deleting message to avoid retries.", invoiceId);
                await SafeDelete(queueUrl, msg, ct);
                return;
            }

            // Idempotency: if already generated, just delete message
            if (!string.IsNullOrWhiteSpace(invoice.PdfS3Key))
            {
                _logger.LogInformation(
                    "Invoice {InvoiceId} already has PdfS3Key={Key}. Deleting message.",
                    invoiceId, invoice.PdfS3Key);

                await SafeDelete(queueUrl, msg, ct);
                return;
            }

            var bucket = _aws.S3?.BucketName;
            if (string.IsNullOrWhiteSpace(bucket))
                throw new InvalidOperationException("Aws:S3:BucketName is missing.");
            var customer = await db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == invoice.CustomerId, ct);

            var pdfBytes = InvoicePdfRenderer.Render(invoice, customer);

            var key = $"invoices/{invoiceId}.pdf";

            await using var ms = new MemoryStream(pdfBytes);
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = ms,
                ContentType = "application/pdf"
            }, ct);

            try
            {
                invoice.AttachPdf(key);
                await db.SaveChangesAsync(ct);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain rule prevented attaching PDF for InvoiceId={InvoiceId}", invoiceId);
            }

            await SafeDelete(queueUrl, msg, ct);
            _logger.LogInformation("Processed invoice job {InvoiceId} -> {Key}", invoiceId, key);
        }
        catch (AmazonS3Exception ex) when (IsBucketNotReady(ex))
        {
            // Bubble up to worker loop so it can back off (prevents tight retries & log spam).
            throw new TransientWorkerException($"S3 bucket not ready while processing InvoiceId={invoiceId}.", ex);
        }
        catch (Exception ex)
        {
            // For transient errors (S3/SQS/DB), keep message so it can retry.
            // NOTE: message will become visible again after VisibilityTimeout.
            throw new TransientWorkerException(
                $"Failed processing invoice message. MessageId={msg.MessageId} InvoiceId={invoiceId}.", ex);
        }
    }

    private async Task<JsonDocument?> ParseOrDeleteAsync(Message msg, string queueUrl, CancellationToken ct)
    {
        try
        {
            return JsonDocument.Parse(msg.Body);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON. Deleting message. MessageId={MessageId}", msg.MessageId);
            await SafeDelete(queueUrl, msg, ct);
            return null;
        }
    }

    private sealed class TransientWorkerException : Exception
    {
        public TransientWorkerException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    private sealed class ExponentialBackoff
    {
        private readonly TimeSpan _min;
        private readonly TimeSpan _max;
        private int _attempt;

        public ExponentialBackoff(TimeSpan min, TimeSpan max)
        {
            if (min <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(min));
            if (max < min) throw new ArgumentOutOfRangeException(nameof(max));

            _min = min;
            _max = max;
            _attempt = 0;
        }

        public int Attempt => _attempt;

        public void Reset() => _attempt = 0;

        public TimeSpan NextDelayWithJitter()
        {
            // attempt 1 => min, attempt 2 => 2*min, attempt 3 => 4*min ... capped at max
            _attempt++;
            var exponent = Math.Min(_attempt - 1, 10); // hard cap exponent to avoid overflow
            var baseDelayMs = _min.TotalMilliseconds * Math.Pow(2, exponent);
            var cappedMs = Math.Min(baseDelayMs, _max.TotalMilliseconds);

            // +/- 20% jitter
            var jitterFactor = 0.8 + (Random.Shared.NextDouble() * 0.4);
            var jitteredMs = cappedMs * jitterFactor;

            return TimeSpan.FromMilliseconds(Math.Max(_min.TotalMilliseconds, jitteredMs));
        }
    }
}
