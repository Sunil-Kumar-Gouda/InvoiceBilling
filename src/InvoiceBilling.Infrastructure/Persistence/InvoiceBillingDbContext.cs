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
    }
}
