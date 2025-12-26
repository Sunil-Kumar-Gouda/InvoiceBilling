namespace InvoiceBilling.Infrastructure.Cloud;

public sealed class AwsOptions
{
    public string ServiceUrl { get; set; } = default!;
    public string Region { get; set; } = "ap-south-1";

    public S3Options S3 { get; set; } = new();
    public SqsOptions Sqs { get; set; } = new();

    public sealed class S3Options { public string BucketName { get; set; } = default!; }
    public sealed class SqsOptions { public string QueueName { get; set; } = default!; }
}
