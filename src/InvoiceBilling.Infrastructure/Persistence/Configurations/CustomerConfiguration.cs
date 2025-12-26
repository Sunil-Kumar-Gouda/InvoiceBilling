using InvoiceBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBilling.Infrastructure.Persistence.Configurations;

//created CustomerConfiguration.cs separately to keep the DbContext clean, keep each entity’s mapping in its own place, and follow the common enterprise EF Core pattern that scales well when your model grows.
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Email)
            .HasMaxLength(200);

        builder.Property(c => c.Phone)
            .HasMaxLength(50);

        builder.Property(c => c.TaxId)
            .HasMaxLength(50);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
