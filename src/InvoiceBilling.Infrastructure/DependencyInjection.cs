using InvoiceBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using InvoiceBilling.Infrastructure.Cloud;
using Microsoft.Extensions.Options;
using InvoiceBilling.Domain.Services;
using Microsoft.Extensions.Hosting;

namespace InvoiceBilling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Use API project's content root so path is stable for dotnet ef
        var dataDir = Path.Combine(environment.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "invoicebilling.db");

        // Prefer config if present, otherwise default to the stable path
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = $"Data Source={dbPath}";

        services.AddDbContext<InvoiceBillingDbContext>(options =>
            options.UseSqlite(connectionString));
        
        // Bind AwsOptions from configuration
        services.Configure<AwsOptions>(configuration.GetSection("Aws"));

        // Validate bound options at startup
        services.AddOptions<AwsOptions>()
            .Validate(o => !string.IsNullOrWhiteSpace(o.ServiceUrl), "Aws:ServiceUrl is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.S3?.BucketName), "Aws:S3:BucketName is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Sqs?.QueueName), "Aws:Sqs:QueueName is required")
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
            var cfg = new AmazonS3Config { ServiceURL = opt.ServiceUrl, ForcePathStyle = true };
            return new AmazonS3Client(new BasicAWSCredentials("test", "test"), cfg);
        });

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
            var cfg = new AmazonSQSConfig { ServiceURL = opt.ServiceUrl };
            return new AmazonSQSClient(new BasicAWSCredentials("test", "test"), cfg);
        });
        services.AddSingleton<IInvoiceTotalsCalculator, InvoiceTotalsCalculator>();

        return services;
    }
}
