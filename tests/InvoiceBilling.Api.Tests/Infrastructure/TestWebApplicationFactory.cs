using InvoiceBilling.Api.Background;
using InvoiceBilling.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InvoiceBilling.Api.Tests.Infrastructure;

/// <summary>
/// Test host that:
/// - disables background workers (SQS polling)
/// - swaps the production SQLite file DB for an in-memory SQLite DB
/// - applies migrations once the host is built
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureServices(services =>
        {
            // 1) Disable the hosted worker so tests don't depend on LocalStack / SQS
            var workerDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(InvoicePdfWorker))
                .ToList();

            foreach (var d in workerDescriptors)
                services.Remove(d);

            // 2) Replace the application's DbContext with an in-memory SQLite DB.
            // Remove existing DbContext registrations first.
            var dbOptionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<InvoiceBillingDbContext>));
            if (dbOptionsDescriptor is not null)
                services.Remove(dbOptionsDescriptor);

            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(InvoiceBillingDbContext));
            if (dbContextDescriptor is not null)
                services.Remove(dbContextDescriptor);

            // Keep the connection open for the lifetime of the factory, otherwise the in-memory DB is lost.
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddSingleton(_connection);
            services.AddDbContext<InvoiceBillingDbContext>(opt => opt.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();

        // Apply migrations on the real host service provider (not a throwaway provider).
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
        }
    }
}
