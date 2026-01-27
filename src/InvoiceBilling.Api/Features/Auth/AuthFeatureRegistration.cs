using System.Text;
using System.Text.Json;
using InvoiceBilling.Api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace InvoiceBilling.Api.Features.Auth;

public static class AuthFeatureRegistration
{
    public static void AddAuthFeature(IServiceCollection services, IConfiguration configuration, bool authEnabled)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<JwtTokenService>();

        if (authEnabled)
        {
            // Validate JWT config when enabled
            services.AddOptions<JwtOptions>()
                .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= 32,
                    "Jwt:SigningKey must be configured and at least 32 characters.")
                .ValidateOnStart();

            var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwt.Issuer,
                        ValidAudience = jwt.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                    // Return RFC7807 payloads for 401/403 so UI can render errors consistently.
                    options.Events = new JwtBearerEvents
                    {
                        OnChallenge = async ctx =>
                        {
                            ctx.HandleResponse();

                            var problem = new ProblemDetails
                            {
                                Status = StatusCodes.Status401Unauthorized,
                                Title = "Unauthorized",
                                Detail = "A valid Bearer token is required to access this resource.",
                                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                                Instance = ctx.Request.Path
                            };
                            problem.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;

                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            ctx.Response.ContentType = "application/problem+json";
                            await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem));
                        },
                        OnForbidden = async ctx =>
                        {
                            var problem = new ProblemDetails
                            {
                                Status = StatusCodes.Status403Forbidden,
                                Title = "Forbidden",
                                Detail = "You are not allowed to access this resource.",
                                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                                Instance = ctx.Request.Path
                            };
                            problem.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;

                            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                            ctx.Response.ContentType = "application/problem+json";
                            await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem));
                        }
                    };
                });

            services.AddAuthorization();
        }
        else
        {
            // Keep tests frictionless: allow everything even if [Authorize] exists.
            services.AddAuthorization(options =>
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAssertion(_ => true)
                    .Build());
        }
    }

    public static void UseAuthFeature(IApplicationBuilder app, bool authEnabled)
    {
        if (authEnabled)
        {
            app.UseAuthentication();
        }

        app.UseAuthorization();
    }
}
