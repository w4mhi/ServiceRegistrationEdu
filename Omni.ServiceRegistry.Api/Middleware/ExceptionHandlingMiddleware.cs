using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Net;
using System.Text.Json;

namespace Omni.ServiceRegistry.Api.Middleware;

/// <summary>
/// Middleware for handling exceptions globally and returning standardized error responses
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ExceptionHandlingMiddleware> logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        this.next = next ?? throw new ArgumentNullException(nameof(next));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        try
        {
            await next(context);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Invalid operation: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex, HttpStatusCode.BadRequest);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid argument: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex, HttpStatusCode.BadRequest);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Resource not found: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex, HttpStatusCode.NotFound);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized access: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex, HttpStatusCode.Forbidden);
        }
#pragma warning disable CA1031 // Do not catch general exception types - this is a global error handler
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex, HttpStatusCode.InternalServerError);
        }
#pragma warning restore CA1031
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, HttpStatusCode statusCode)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        ProblemDetails problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = statusCode.ToString(),
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        string json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
