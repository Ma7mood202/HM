using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Hm.WebApi.Middlewares;

/// <summary>
/// Catches unhandled exceptions, logs them, and returns a consistent JSON error response.
/// In Development, actual exception details are returned to aid debugging.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, includeDetails) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, exception.Message, true),
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message, true),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message, true),
            ArgumentException or ArgumentNullException => (HttpStatusCode.BadRequest, exception.Message, true),
            DbUpdateException dbEx => (
                HttpStatusCode.InternalServerError,
                _env.IsDevelopment() ? GetDbExceptionMessage(dbEx) : "A database error occurred. Please try again.",
                _env.IsDevelopment()
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                _env.IsDevelopment() ? exception.Message : "An error occurred.",
                _env.IsDevelopment()
            )
        };

        // Always log the full exception server-side for debugging
        _logger.LogError(exception, "Unhandled exception: {Message}. Path: {Path}", exception.Message, context.Request.Path);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        object response;
        if (includeDetails && _env.IsDevelopment())
        {
            response = new
            {
                statusCode = (int)statusCode,
                message,
                detail = exception.InnerException?.Message,
                type = exception.GetType().Name
            };
        }
        else
        {
            response = new
            {
                statusCode = (int)statusCode,
                message
            };
        }

        var json = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(json);
    }

    private static string GetDbExceptionMessage(DbUpdateException ex)
    {
        var inner = ex.InnerException?.Message ?? ex.Message;
        // Avoid exposing raw SQL; summarize the error
        if (inner.Contains("duplicate key") || inner.Contains("unique constraint"))
            return "A record with this value already exists.";
        if (inner.Contains("foreign key") || inner.Contains("reference"))
            return "Invalid reference: related data may not exist.";
        return $"Database error: {inner}";
    }
}
