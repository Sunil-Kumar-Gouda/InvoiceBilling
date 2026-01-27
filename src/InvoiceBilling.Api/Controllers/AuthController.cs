using InvoiceBilling.Api.Auth;
using InvoiceBilling.Api.Dtos.Auth;
using InvoiceBilling.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceBilling.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly JwtTokenService _jwt;

    public AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, JwtTokenService jwt)
    {
        _users = users;
        _signIn = signIn;
        _jwt = jwt;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["email"] = new[] { "Email is required." } }));

        if (string.IsNullOrWhiteSpace(request.Password))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["password"] = new[] { "Password is required." } }));

        var existing = await _users.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Email already registered",
                Detail = "A user with this email already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim()
        };

        var result = await _users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            // Return Identity errors in a ValidationProblemDetails-compatible shape.
            var errors = result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        // No roles by default; extend later (Admin/User, per-tenant roles, etc.).
        var roles = await _users.GetRolesAsync(user);
        var (token, expiresAtUtc) = _jwt.CreateToken(user, roles);

        return Ok(new AuthResponse
        {
            AccessToken = token,
            ExpiresAtUtc = expiresAtUtc,
            UserId = user.Id.ToString(),
            Email = user.Email ?? request.Email,
            DisplayName = user.DisplayName,
            Roles = roles.ToArray()
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Email)) errors["email"] = new[] { "Email is required." };
            if (string.IsNullOrWhiteSpace(request.Password)) errors["password"] = new[] { "Password is required." };
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var user = await _users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid credentials",
                Detail = "Email or password is incorrect.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var signIn = await _signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!signIn.Succeeded)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid credentials",
                Detail = "Email or password is incorrect.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var roles = await _users.GetRolesAsync(user);
        var (token, expiresAtUtc) = _jwt.CreateToken(user, roles);

        return Ok(new AuthResponse
        {
            AccessToken = token,
            ExpiresAtUtc = expiresAtUtc,
            UserId = user.Id.ToString(),
            Email = user.Email ?? request.Email,
            DisplayName = user.DisplayName,
            Roles = roles.ToArray()
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var id))
            return Unauthorized();

        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null)
            return Unauthorized();

        var roles = await _users.GetRolesAsync(user);

        return Ok(new AuthResponse
        {
            AccessToken = string.Empty,
            ExpiresAtUtc = DateTime.UtcNow,
            UserId = user.Id.ToString(),
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            Roles = roles.ToArray()
        });
    }
}
