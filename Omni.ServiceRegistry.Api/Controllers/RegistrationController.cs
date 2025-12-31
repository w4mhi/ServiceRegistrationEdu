using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Omni.ServiceRegistry.Api.DTOs;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Api.Controllers;

/// <summary>
/// Registration submission endpoint (US1, R1-R10)
/// </summary>
[ApiController]
[Route("api/v1")]
public class RegistrationController : ControllerBase
{
    private readonly IRegistrationService registrationService;
    private readonly IAdministratorService administratorService;
    private readonly ILogger<RegistrationController> logger;

    public RegistrationController(
        IRegistrationService registrationService,
        IAdministratorService administratorService,
        ILogger<RegistrationController> logger)
    {
        this.registrationService = registrationService;
        this.administratorService = administratorService;
        this.logger = logger;
    }

    /// <summary>
    /// Submit service registration request (R1)
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>Registration ID and status</returns>
    /// <response code="429">Rate limit exceeded</response>
    [HttpPost("register")]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(typeof(RegistrationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RegistrationResponseDto>> Register([FromBody] RegistrationRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Serialize endpoints to JSON
            string endpointsJson = JsonSerializer.Serialize(request.Endpoints);
            string? apiEndpointsJson = request.ApiEndpoints != null ? JsonSerializer.Serialize(request.ApiEndpoints) : null;

            RegistrationRequest result = await registrationService.SubmitRegistrationAsync(
                request.ServiceName,
                request.Description,
                request.ContactEmail,
                endpointsJson,
                apiEndpointsJson,
                request.HeartbeatTimeout,
                request.MaxMissedHeartbeats);

            stopwatch.Stop();
            
            logger.LogInformation(
                "Registration request submitted: {RegistrationId} for service {ServiceName} (Duration: {DurationMs}ms, Target: <2000ms)",
                result.RegistrationId, result.ServiceName, stopwatch.ElapsedMilliseconds);
            
            if (stopwatch.ElapsedMilliseconds >= 2000)
            {
                logger.LogWarning(
                    "Registration performance degraded: {DurationMs}ms exceeded 2000ms target for {ServiceName}",
                    stopwatch.ElapsedMilliseconds, result.ServiceName);
            }

            return Ok(new RegistrationResponseDto
            {
                RegistrationId = result.RegistrationId,
                Status = result.Status.ToString(),
                Message = "Registration request submitted successfully. Awaiting administrator approval.",
                ServiceId = result.ServiceId
            });
        }
        catch (ArgumentException ex)
        {
            stopwatch.Stop();
            logger.LogWarning(ex, "Validation failed for registration request: {ServiceName} (Duration: {DurationMs}ms)", 
                request.ServiceName, stopwatch.ElapsedMilliseconds);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = ex.Message
            });
        }
#pragma warning disable CA1031 // This is a catch-all for unexpected errors
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Unexpected error during registration for service: {ServiceName} (Duration: {DurationMs}ms)", 
                request.ServiceName, stopwatch.ElapsedMilliseconds);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service Unavailable",
                Detail = "An error occurred while processing the registration. Please try again later."
            });
        }
    }
}
