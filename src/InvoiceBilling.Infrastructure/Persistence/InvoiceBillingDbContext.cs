using InvoiceBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Infrastructure.Persistence;

public class InvoiceBillingDbContext : DbContext
{
    public InvoiceBillingDbContext(DbContextOptions<InvoiceBillingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        /* typeof(InvoiceBillingDbContext) → gets the System.Type object for your DbContext class.
        .Assembly → gets the System.Reflection.Assembly where that type is defined. */
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoiceBillingDbContext).Assembly);
    }
}
