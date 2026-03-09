using Amazon.S3;
using Amazon.S3.Model;
using InvoiceBilling.Application.Common.Storage;
using InvoiceBilling.Infrastructure.Cloud;
using Microsoft.Extensions.Options;
using System.Net;

namespace InvoiceBilling.Infrastructure.Storage;

/// <summary>
/// AWS S3-backed implementation of <see cref="IInvoicePdfStorage"/>.
/// </summary>
public sealed class S3InvoicePdfStorage : IInvoicePdfStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;

    public S3InvoicePdfStorage(IAmazonS3 s3, IOptions<AwsOptions> awsOptions)
    {
        _s3 = s3;
        _bucketName = awsOptions.Value.S3?.BucketName
            ?? throw new InvalidOperationException("AWS S3 bucket configuration missing (Aws:S3:BucketName).");
    }

    public async Task<InvoicePdfDownload?> TryDownloadAsync(
        string s3Key,
        string invoiceNumber,
        CancellationToken ct)
    {
        try
        {
            var response = await _s3.GetObjectAsync(
                new GetObjectRequest { BucketName = _bucketName, Key = s3Key },
                ct);

            var contentType = ResolveContentType(s3Key) ?? response.Headers.ContentType ?? "application/octet-stream";
            var ext = Path.GetExtension(s3Key);
            var fileName = string.IsNullOrWhiteSpace(ext)
                ? invoiceNumber
                : $"{invoiceNumber}{ext}";

            return new InvoicePdfDownload(response.ResponseStream, contentType, fileName);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    private static string? ResolveContentType(string key) =>
        Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            _ => null
        };
}
