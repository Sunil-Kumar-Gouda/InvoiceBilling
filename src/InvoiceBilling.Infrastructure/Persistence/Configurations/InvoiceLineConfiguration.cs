using InvoiceBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBilling.Infrastructure.Persistence.Configurations;

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();

        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.Quantity).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);

        // Relationships
        builder.HasOne<Product>()
               .WithMany()
               .HasForeignKey(x => x.ProductId)
               // Do not allow deleting products referenced by invoices
               .OnDelete(DeleteBehavior.Restrict);

        // Query indexes
        builder.HasIndex(x => x.InvoiceId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => new { x.InvoiceId, x.ProductId });
    }
}
