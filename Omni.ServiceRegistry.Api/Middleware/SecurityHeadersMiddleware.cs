using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Api.Middleware;

/// <summary>
/// Middleware to add security headers to HTTP responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<SecurityHeadersMiddleware> logger;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger)
    {
        this.next = next ?? throw new ArgumentNullException(nameof(next));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        // Add HSTS (HTTP Strict Transport Security)
        if (!context.Response.Headers.ContainsKey("Strict-Transport-Security"))
        {
            context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }

        // Prevent MIME type sniffing
        if (!context.Response.Headers.ContainsKey("X-Content-Type-Options"))
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        }

        // Prevent clickjacking attacks
        if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
        {
            context.Response.Headers.Append("X-Frame-Options", "DENY");
        }

        // Content Security Policy
        if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
        {
            context.Response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'");
        }

        // Referrer Policy
        if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
        {
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        }

        // Permissions Policy (formerly Feature Policy)
        if (!context.Response.Headers.ContainsKey("Permissions-Policy"))
        {
            context.Response.Headers.Append("Permissions-Policy",
                "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
        }

        logger.LogTrace("Security headers added to response for {Path}", context.Request.Path);

        await next(context);
    }
}
