using InvoiceBilling.Infrastructure.Persistence;
using InvoiceBilling.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Amazon;
using InvoiceBilling.Infrastructure.PdfTemplates;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using InvoiceBilling.Infrastructure.Cloud;
using InvoiceBilling.Infrastructure.Standalone;
using Microsoft.Extensions.Options;
using InvoiceBilling.Domain.Services;
using Microsoft.Extensions.Hosting;
using InvoiceBilling.Application.Common.Jobs;
using InvoiceBilling.Application.Common.PdfTemplates;
using InvoiceBilling.Application.Common.Storage;
using InvoiceBilling.Infrastructure.Auth;
using InvoiceBilling.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;

namespace InvoiceBilling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Database: SQLite (no server needed in either mode).
        var dataDir = Path.Combine(environment.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "invoicebilling.db");

        // Prefer config if present, otherwise default to the stable path
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = $"Data Source={dbPath}";

        services.AddDbContext<InvoiceBillingDbContext>(options =>
            options.UseSqlite(connectionString));

        // Identity (users) stored alongside the app database.
        // JWT bearer authentication is configured in the API project.
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Reasonable defaults for a demo/prototype; tighten for production.
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<InvoiceBillingDbContext>()
            .AddSignInManager<SignInManager<ApplicationUser>>();
        //.AddSignInManager();

        // Expose a minimal abstraction for Application handlers.
        services.AddScoped<IInvoiceBillingDbContext>(sp =>
            sp.GetRequiredService<InvoiceBillingDbContext>());

        // PDF template storage (file-based) + renderer (shared by both modes).
        services.Configure<PdfTemplatesOptions>(configuration.GetSection(PdfTemplatesOptions.SectionName));
        services.AddSingleton<IPdfTemplateStore, FilePdfTemplateStore>();
        services.AddSingleton<IActivePdfTemplateStore>(sp => (IActivePdfTemplateStore)sp.GetRequiredService<IPdfTemplateStore>());
        services.AddSingleton<InvoiceValueResolver>();
        services.AddScoped<IInvoicePdfTemplateRenderer, InvoicePdfTemplateRenderer>();
        services.AddScoped<IInvoicePdfPreviewRenderer, InvoicePdfPreviewRenderer>();

        // Domain services (stateless, shared).
        services.AddSingleton<IInvoiceTotalsCalculator, InvoiceTotalsCalculator>();

        // ── Mode-specific registrations ───────────────────────────────────

        var mode = configuration.GetValue<InfrastructureMode>("Infrastructure:Mode");

        if (mode == InfrastructureMode.Standalone)
            AddStandaloneServices(services, configuration);
        else
            AddCloudServices(services, configuration);

        return services;
    }

    /// <summary>
    /// Registers AWS-backed services: S3 client, SQS client, and their consumers.
    /// Requires Docker + LocalStack (or real AWS credentials) at runtime.
    /// </summary>
    private static void AddCloudServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AwsOptions>(configuration.GetSection("Aws"));

        services.AddOptions<AwsOptions>()
            .Validate(o => !string.IsNullOrWhiteSpace(o.ServiceUrl), "Aws:ServiceUrl is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.S3?.BucketName), "Aws:S3:BucketName is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Sqs?.QueueName), "Aws:Sqs:QueueName is required")
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<AwsOptions>>().Value;

            var cfg = new AmazonS3Config
            {
                ServiceURL = opt.ServiceUrl,
                ForcePathStyle = true,
                //RegionEndpoint = region
                AuthenticationRegion = opt.Region
            };

            return new AmazonS3Client(new BasicAWSCredentials("test", "test"), cfg);
        });

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<AwsOptions>>().Value;

            var cfg = new AmazonSQSConfig
            {
                ServiceURL = opt.ServiceUrl,
                //RegionEndpoint = region,
                AuthenticationRegion = opt.Region
            };

            return new AmazonSQSClient(new BasicAWSCredentials("test", "test"), cfg);
        });

        services.AddSingleton<IInvoicePdfJobEnqueuer, SqsInvoicePdfJobEnqueuer>();
        services.AddSingleton<IInvoicePdfStorage, S3InvoicePdfStorage>();
    }

    /// <summary>
    /// Registers in-process, filesystem-backed services.
    /// Zero external dependencies — no Docker, no LocalStack, no AWS credentials.
    /// </summary>
    private static void AddStandaloneServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StandaloneOptions>(configuration.GetSection(StandaloneOptions.SectionName));

        services.AddOptions<StandaloneOptions>()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.PdfStoragePath),
                "Infrastructure:Standalone:PdfStoragePath must not be empty.")
            .ValidateOnStart();

        // Single channel instance shared between the enqueuer and the worker.
        services.AddSingleton<InProcessPdfJobChannel>();
        services.AddSingleton<IInvoicePdfJobEnqueuer, InProcessPdfJobEnqueuer>();

        // Register the concrete type so the InProcessPdfWorker can resolve file paths,
        // then forward the interface to the same singleton instance.
        services.AddSingleton<LocalFileInvoicePdfStorage>();
        services.AddSingleton<IInvoicePdfStorage>(sp =>
            sp.GetRequiredService<LocalFileInvoicePdfStorage>());
    }
}
