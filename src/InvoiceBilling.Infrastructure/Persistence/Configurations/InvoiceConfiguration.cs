using InvoiceBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBilling.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceNumber).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();

        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();

        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxTotal).HasPrecision(18, 2);
        builder.Property(x => x.GrandTotal).HasPrecision(18, 2);

        builder.Property(x => x.PdfS3Key).HasMaxLength(512);
        builder.Property(x => x.TaxRatePercent).HasPrecision(5, 2);

        // Relationships
        builder.HasOne<Customer>()
               .WithMany()
               .HasForeignKey(x => x.CustomerId)
               // Invoicing data should not be cascade-deleted
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
               .WithOne()
               .HasForeignKey(l => l.InvoiceId)
               .OnDelete(DeleteBehavior.Cascade);

        // Query indexes (common list filters)
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.IssueDate);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.CustomerId, x.Status });

        builder.Navigation(x => x.Lines)
       .HasField("_lines")
       .UsePropertyAccessMode(PropertyAccessMode.Field);

    }
}
