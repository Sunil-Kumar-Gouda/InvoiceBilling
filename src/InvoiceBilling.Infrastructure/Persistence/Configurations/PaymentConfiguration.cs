using InvoiceBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBilling.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Amount).HasPrecision(18, 2);

        builder.Property(x => x.Method).HasMaxLength(32);
        builder.Property(x => x.Reference).HasMaxLength(64);
        builder.Property(x => x.Note).HasMaxLength(512);

        // Constraints (DB-level safety net)
        // (SQLite): decimals are stored as TEXT; "+0" forces numeric comparison.
        builder.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("CK_Payments_Amount_Positive", "(Amount + 0) > 0");
        });

        // Relationships
        builder.HasOne<Invoice>()
               .WithMany()
               .HasForeignKey(x => x.InvoiceId)
               .OnDelete(DeleteBehavior.Cascade);

        // Query indexes
        builder.HasIndex(x => x.InvoiceId);
        builder.HasIndex(x => x.PaidAt);
        builder.HasIndex(x => new { x.InvoiceId, x.PaidAt });
    }
}
