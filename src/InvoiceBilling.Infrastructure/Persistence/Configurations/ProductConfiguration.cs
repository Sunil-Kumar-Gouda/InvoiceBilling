using InvoiceBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBilling.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(64);
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();

        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);

        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.Sku).IsUnique().HasFilter("[Sku] IS NOT NULL");
    }
}
