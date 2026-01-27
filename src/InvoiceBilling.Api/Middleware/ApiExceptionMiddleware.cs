using System.Text.Json;
using FluentValidation;
using InvoiceBilling.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Api.Middleware;

/// <summary>
/// Ensures the API returns consistent RFC7807 ProblemDetails payloads.
///
/// Mappings:
/// - FluentValidation.ValidationException -> 400 ValidationProblemDetails (errors dictionary)
/// - DomainException -> 409 ProblemDetails
/// - DbUpdateConcurrencyException -> 409 ProblemDetails
/// - any other exception -> 500 ProblemDetails
/// </summary>
public sealed class ApiExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await WriteValidationProblemAsync(context, ex);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                title: "Domain conflict",
                detail: ex.Message);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                title: "Concurrency conflict",
                detail: "The resource was updated by another request. Please retry.");

            _logger.LogWarning(ex, "DbUpdateConcurrencyException");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                title: "Unexpected error",
                detail: "An unexpected error occurred.");
        }
    }

    private static async Task WriteValidationProblemAsync(HttpContext context, ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => string.IsNullOrWhiteSpace(e.PropertyName) ? "request" : e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        var pd = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = string.Join(Environment.NewLine, errors.SelectMany(kv => kv.Value)),
            Instance = context.Request.Path
        };

        pd.Extensions["traceId"] = context.TraceIdentifier;

        await WriteProblemDetailsAsync(context, pd);
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        var pd = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        pd.Extensions["traceId"] = context.TraceIdentifier;

        await WriteProblemDetailsAsync(context, pd);
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, ProblemDetails pd)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = pd.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(pd, JsonOptions));
    }
}