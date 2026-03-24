using InvoiceBilling.Api.Tests.Infrastructure;
using InvoiceBilling.Application.Common.Jobs;
using InvoiceBilling.Application.Common.Storage;
using InvoiceBilling.Infrastructure.Persistence;
using InvoiceBilling.Infrastructure.Standalone;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InvoiceBilling.Api.Tests.Standalone;

/// <summary>
/// Tests that verify the DI container wires the correct implementations
/// based on the <c>Infrastructure:Mode</c> configuration value.
/// </summary>
public sealed class StandaloneDependencyInjectionTests : IClassFixture<StandaloneWebApplicationFactory>
{
    private readonly StandaloneWebApplicationFactory _factory;

    public StandaloneDependencyInjectionTests(StandaloneWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Standalone_mode_resolves_InProcessPdfJobEnqueuer_for_IInvoicePdfJobEnqueuer()
    {
        using var scope = _factory.Services.CreateScope();
        var enqueuer = scope.ServiceProvider.GetRequiredService<IInvoicePdfJobEnqueuer>();

        Assert.IsType<InProcessPdfJobEnqueuer>(enqueuer);
    }

    [Fact]
    public void Standalone_mode_resolves_LocalFileInvoicePdfStorage_for_IInvoicePdfStorage()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IInvoicePdfStorage>();

        Assert.IsType<LocalFileInvoicePdfStorage>(storage);
    }

    [Fact]
    public void Standalone_mode_registers_InProcessPdfJobChannel_as_singleton()
    {
        var first = _factory.Services.GetRequiredService<InProcessPdfJobChannel>();
        var second = _factory.Services.GetRequiredService<InProcessPdfJobChannel>();

        Assert.Same(first, second);
    }

    [Fact]
    public void Standalone_mode_registers_LocalFileInvoicePdfStorage_as_singleton()
    {
        // The concrete type and the interface must resolve to the same instance.
        var concrete = _factory.Services.GetRequiredService<LocalFileInvoicePdfStorage>();
        var viaInterface = _factory.Services.GetRequiredService<IInvoicePdfStorage>();

        Assert.Same(concrete, viaInterface);
    }

    [Fact]
    public void Standalone_mode_does_not_register_AWS_services()
    {
        // IAmazonS3 and IAmazonSQS should NOT be in the container in standalone mode.
        var s3 = _factory.Services.GetService(typeof(Amazon.S3.IAmazonS3));
        var sqs = _factory.Services.GetService(typeof(Amazon.SQS.IAmazonSQS));

        Assert.Null(s3);
        Assert.Null(sqs);
    }

    [Fact]
    public async Task Standalone_mode_health_endpoint_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>
/// Test host for standalone mode.
/// Mirrors <see cref="TestWebApplicationFactory"/> (in-memory SQLite, disabled auth,
/// no background workers) but replaces cloud service registrations (S3, SQS) with
/// standalone implementations (in-process channel, local filesystem).
/// </summary>
/// <remarks>
/// Configuration overrides via <c>ConfigureAppConfiguration</c> arrive AFTER
/// <c>Program.cs</c> has already called <c>AddInfrastructure(builder.Configuration)</c>,
/// which reads <c>Infrastructure:Mode</c> and registers cloud services. Therefore we
/// must manually remove the cloud registrations and add standalone ones in
/// <c>ConfigureServices</c> — the same pattern <see cref="TestWebApplicationFactory"/>
/// uses to swap the DbContext.
/// </remarks>
public sealed class StandaloneWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _pdfDir;
    private SqliteConnection? _connection;

    public StandaloneWebApplicationFactory()
    {
        _pdfDir = Path.Combine(Path.GetTempPath(), $"InvoiceBilling_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_pdfDir);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Enabled"] = "false",
                ["BackgroundWorkers:InvoicePdfWorker:Enabled"] = "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            // ── Disable background workers ──────────────────────────────
            var workerDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                         && (d.ImplementationType?.Name == "InvoicePdfWorker"
                          || d.ImplementationType?.Name == "InProcessPdfWorker"))
                .ToList();

            foreach (var d in workerDescriptors)
                services.Remove(d);

            // ── Swap file-based SQLite for in-memory SQLite ─────────────
            var dbOptionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<InvoiceBillingDbContext>));
            if (dbOptionsDescriptor is not null)
                services.Remove(dbOptionsDescriptor);

            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(InvoiceBillingDbContext));
            if (dbContextDescriptor is not null)
                services.Remove(dbContextDescriptor);

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddSingleton(_connection);
            services.AddDbContext<InvoiceBillingDbContext>(opt => opt.UseSqlite(_connection));

            // ── Swap cloud services for standalone implementations ──────
            // Program.cs already registered S3/SQS services because it read
            // Infrastructure:Mode=Cloud from appsettings.json before our
            // ConfigureAppConfiguration override ran. Remove them and add
            // standalone replacements.

            RemoveService<Amazon.S3.IAmazonS3>(services);
            RemoveService<Amazon.SQS.IAmazonSQS>(services);
            RemoveService<IInvoicePdfJobEnqueuer>(services);
            RemoveService<IInvoicePdfStorage>(services);

            var standaloneOptions = Microsoft.Extensions.Options.Options.Create(
                new StandaloneOptions { PdfStoragePath = _pdfDir });

            services.AddSingleton<InProcessPdfJobChannel>();
            services.AddSingleton<IInvoicePdfJobEnqueuer>(sp =>
                new InProcessPdfJobEnqueuer(
                    sp.GetRequiredService<InProcessPdfJobChannel>(),
                    new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["BackgroundWorkers:InvoicePdfWorker:Enabled"] = "false"
                        })
                        .Build(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<InProcessPdfJobEnqueuer>.Instance));

            services.AddSingleton(sp =>
                new LocalFileInvoicePdfStorage(
                    standaloneOptions,
                    sp.GetRequiredService<IHostEnvironment>(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalFileInvoicePdfStorage>.Instance));

            services.AddSingleton<IInvoicePdfStorage>(sp =>
                sp.GetRequiredService<LocalFileInvoicePdfStorage>());
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvoiceBillingDbContext>();
            db.Database.Migrate();
        }

        host.Start();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
            _connection = null;

            try { Directory.Delete(_pdfDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in descriptors)
            services.Remove(d);
    }
}
