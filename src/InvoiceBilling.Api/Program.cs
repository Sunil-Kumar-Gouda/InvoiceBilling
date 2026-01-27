using FluentValidation;
using InvoiceBilling.Api.Features.Auth;
using InvoiceBilling.Api.Features.Common;
using InvoiceBilling.Application.Invoices.IssueInvoice;
using InvoiceBilling.Application.Invoices.UpdateDraftInvoice;
using InvoiceBilling.Infrastructure;
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

// API error payloads (RFC7807 ProblemDetails + consistent ModelState errors)
ApiErrorFeatureRegistration.AddApiErrorFeature(builder.Services);

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

// Auth foundation (JWT) + authorization policy wiring
var authEnabled = builder.Configuration.GetValue<bool>("Auth:Enabled", true);
AuthFeatureRegistration.AddAuthFeature(builder.Services, builder.Configuration, authEnabled);

var app = builder.Build();

app.UseCors("SpaCors");

// ProblemDetails exception mapping middleware
ApiErrorFeatureRegistration.UseApiErrorFeature(app);

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Auth pipeline (conditionally enables authentication, always applies authorization)
AuthFeatureRegistration.UseAuthFeature(app, authEnabled);

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
