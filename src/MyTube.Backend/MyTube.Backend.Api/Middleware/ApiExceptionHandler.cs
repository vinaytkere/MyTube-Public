using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MyTube.Backend.Api.Middleware;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "Validation Failed",
                string.Join("; ", validationEx.Errors?.Select(e => e.ErrorMessage) ?? new[] { validationEx.Message })),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found", "The requested resource was not found."),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "Access denied."),
            ArgumentException argEx => (StatusCodes.Status400BadRequest, "Bad Request", argEx.Message),
            InvalidOperationException invEx => (StatusCodes.Status409Conflict, "Conflict", invEx.Message),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled API exception: {Title}", title);
        }
        else
        {
            logger.LogWarning(exception, "Handled API exception: {Title}", title);
        }

        httpContext.Response.StatusCode = statusCode;

        // Ensure CORS headers are included on error responses for web client
        var origin = httpContext.Request.Headers["Origin"].ToString();
        var allowedOrigin = !string.IsNullOrWhiteSpace(origin) ? origin : "*";
        httpContext.Response.Headers["Access-Control-Allow-Origin"] = allowedOrigin;
        if (allowedOrigin != "*")
        {
            httpContext.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        }

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        }, cancellationToken);

        return true;
    }
}
