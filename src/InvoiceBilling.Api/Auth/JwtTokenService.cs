using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InvoiceBilling.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InvoiceBilling.Api.Auth;

public sealed class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateToken(ApplicationUser user, IEnumerable<string> roles)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
            throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id.ToString())
        };

        foreach (var r in roles)
            claims.Add(new Claim(ClaimTypes.Role, r));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return (jwt, expiresAtUtc);
    }
}
