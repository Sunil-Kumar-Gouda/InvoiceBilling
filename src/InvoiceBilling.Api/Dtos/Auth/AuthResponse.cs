namespace InvoiceBilling.Api.Dtos.Auth;

public sealed class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }

    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
