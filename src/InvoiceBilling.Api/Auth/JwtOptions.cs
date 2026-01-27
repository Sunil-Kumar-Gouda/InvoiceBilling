namespace InvoiceBilling.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "InvoiceBilling";
    public string Audience { get; set; } = "InvoiceBilling";

    /// <summary>
    /// Symmetric signing key for HMAC-SHA256.
    /// Use a long random value (at least 32 characters) in production.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Access token lifetime in minutes.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 60;
}
