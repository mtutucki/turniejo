using Microsoft.AspNetCore.Mvc;
using Turniejo.SharedKernel.Behaviors;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (TurniejoValidationException ex)
        {
            logger.LogWarning("Validation error: {Errors}", ex.Errors);
            await HandleValidationExceptionAsync(context, ex);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Domain error: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex.Message, 422, "Domain Error");
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning("Not found: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex.Message, 404, "Not Found");
        }
        catch (UnauthorizedException ex)
        {
            logger.LogWarning("Unauthorized: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex.Message, 403, "Forbidden");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred");
            await HandleExceptionAsync(context, "An unexpected error occurred.", 500, "Server Error");
        }
    }

    private static async Task HandleValidationExceptionAsync(
        HttpContext context,
        TurniejoValidationException ex)
    {
        var problemDetails = new ProblemDetails
        {
            Status = 422,
            Title = "Validation Error",
            Type = "https://tools.ietf.org/html/rfc4918#section-11.2"
        };

        problemDetails.Extensions["errors"] = ex.Errors
            .Select(e => new { field = e.Field, message = e.Message });

        context.Response.StatusCode = 422;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static async Task HandleExceptionAsync(
        HttpContext context,
        string message,
        int statusCode,
        string title)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = message,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}