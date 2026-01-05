using InvoiceBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace InvoiceBilling.Application.Common.Persistence;

/// <summary>
/// Minimal DbContext abstraction used by Application-layer handlers.
///
/// This intentionally exposes EF Core types so the Application layer can use
/// set-based operations (ExecuteDelete) and transactions via DatabaseFacade.
/// </summary>
public interface IInvoiceBillingDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
