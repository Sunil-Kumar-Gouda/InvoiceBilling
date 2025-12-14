using InvoiceBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceBilling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Use SQLite connection string.
        // If not found in config, fall back to a local file.
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                              ?? "Data Source=invoicebilling.db";

        services.AddDbContext<InvoiceBillingDbContext>(options =>
            options.UseSqlite(connectionString));  
        return services;
    }
}
