using Microsoft.AspNetCore.Identity;

namespace InvoiceBilling.Infrastructure.Auth;

/// <summary>
/// Minimal application user for JWT authentication.
///
/// Notes:
/// - Stored in the same SQLite database as InvoiceBilling aggregates.
/// - Uses Guid primary keys to align with the rest of the system.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
}
