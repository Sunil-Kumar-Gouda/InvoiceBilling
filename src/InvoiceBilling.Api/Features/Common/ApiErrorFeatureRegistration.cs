using InvoiceBilling.Api.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceBilling.Api.Features.Common;

public static class ApiErrorFeatureRegistration
{
    /// <summary>
    /// Registers consistent RFC7807 error payloads.
    /// - Ensures model binding errors produce ValidationProblemDetails.
    /// - Leaves business/exception mapping to ApiExceptionMiddleware.
    /// </summary>
    public static void AddApiErrorFeature(IServiceCollection services)
    {
        services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var problem = new ValidationProblemDetails(context.ModelState)
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "One or more validation errors occurred.",
                        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                        Instance = context.HttpContext.Request.Path
                    };

                    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                    return new BadRequestObjectResult(problem)
                    {
                        ContentTypes = { "application/problem+json" }
                    };
                };
            });
    }

    /// <summary>
    /// Wires middleware that maps exceptions to ProblemDetails.
    /// </summary>
    public static void UseApiErrorFeature(IApplicationBuilder app)
    {
        app.UseMiddleware<ApiExceptionMiddleware>();
    }
}
