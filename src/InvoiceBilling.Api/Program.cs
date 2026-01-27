using System.Text;
using FluentValidation;
using InvoiceBilling.Api.Auth;
using InvoiceBilling.Application.Invoices.IssueInvoice;
using InvoiceBilling.Application.Invoices.UpdateDraftInvoice;
using InvoiceBilling.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("SpaCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Content-Disposition");
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger (+ JWT Bearer)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "InvoiceBilling API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Infrastructure (DbContext, AWS clients, PDF worker deps, Identity)
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// FluentValidation (existing: IssueInvoiceCommand)
builder.Services.AddScoped<IValidator<IssueInvoiceCommand>, IssueInvoiceCommandValidator>();

// CQRS: register MediatR handlers from Application layer
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<UpdateDraftInvoiceCommand>());

builder.Services.AddHealthChecks();

if (builder.Configuration.GetValue<bool>("BackgroundWorkers:InvoicePdfWorker:Enabled"))
{
    builder.Services.AddHostedService<InvoiceBilling.Api.Background.InvoicePdfWorker>();
}

// Auth foundation (JWT)
var authEnabled = builder.Configuration.GetValue<bool>("Auth:Enabled", true);

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddScoped<JwtTokenService>();

if (authEnabled)
{
    // Validate JWT config when enabled
    builder.Services.AddOptions<JwtOptions>()
        .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= 32,
            "Jwt:SigningKey must be configured and at least 32 characters.")
        .ValidateOnStart();

    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

    builder.Services
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
        });

    builder.Services.AddAuthorization();
}
else
{
    // Keep tests frictionless (no auth); still allow [Authorize] in later days.
    builder.Services.AddAuthorization(options =>
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build());
}

var app = builder.Build();

app.UseCors("SpaCors");

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (authEnabled)
{
    app.UseAuthentication();
}

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
