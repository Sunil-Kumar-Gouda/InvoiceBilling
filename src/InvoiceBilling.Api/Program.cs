using FluentValidation;
using InvoiceBilling.Application.Invoices.IssueInvoice;
using InvoiceBilling.Application.Invoices.UpdateDraftInvoice;
using InvoiceBilling.Infrastructure;
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

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Infrastructure (DbContext, etc.)
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// FluentValidation (CQRS commands)
builder.Services.AddScoped<IValidator<IssueInvoiceCommand>, IssueInvoiceCommandValidator>();

// CQRS: register MediatR handlers from Application layer
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<UpdateDraftInvoiceCommand>());

builder.Services.AddHealthChecks();
//builder.Services.AddHostedService<InvoiceBilling.Api.Background.InvoicePdfWorker>();
if (builder.Configuration.GetValue<bool>("BackgroundWorkers:InvoicePdfWorker:Enabled"))
{
    builder.Services.AddHostedService<InvoiceBilling.Api.Background.InvoicePdfWorker>();
}

var app = builder.Build();
app.UseCors("SpaCors");
app.MapControllers();
app.MapHealthChecks("/health");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public partial class Program { }
