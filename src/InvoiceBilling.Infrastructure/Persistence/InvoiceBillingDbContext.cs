using InvoiceBilling.Application.Common.Persistence;
using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Infrastructure.Persistence;

public class InvoiceBillingDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IInvoiceBillingDbContext
{
    public InvoiceBillingDbContext(DbContextOptions<InvoiceBillingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        /* typeof(InvoiceBillingDbContext) → gets the System.Type object for your DbContext class.
        .Assembly → gets the System.Reflection.Assembly where that type is defined. */
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoiceBillingDbContext).Assembly);
    }
}
