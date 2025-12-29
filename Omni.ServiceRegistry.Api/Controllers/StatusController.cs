using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Text.Json;
using Omni.ServiceRegistry.Api.DTOs;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Api.Controllers;

/// <summary>
/// Status query endpoints (US3, US5, R9, R18)
/// </summary>
[ApiController]
[Route("api/v1/status")]
public class StatusController : ControllerBase
{
    private readonly IRegistrationService registrationService;
    private readonly IServiceCatalogService catalogService;
    private readonly ILogger<StatusController> logger;

    public StatusController(
        IRegistrationService registrationService,
        IServiceCatalogService catalogService,
        ILogger<StatusController> logger)
    {
        this.registrationService = registrationService;
        this.catalogService = catalogService;
        this.logger = logger;
    }

    /// <summary>
    /// Get registration status by ID (R9)
    /// </summary>
    /// <param name="id">Registration request ID</param>
    /// <returns>Registration status details</returns>
    [HttpGet("registration/{id}")]
    [ProducesResponseType(typeof(RegistrationStatusResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RegistrationStatusResponseDto>> GetRegistrationStatus(Guid id)
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        RegistrationRequest? request = await registrationService.GetRegistrationStatusAsync(id);

        if (request == null)
        {
            stopwatch.Stop();
            logger.LogWarning("Registration status query failed: {RegistrationId} not found (Duration: {DurationMs}ms)", 
                id, stopwatch.ElapsedMilliseconds);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Registration Not Found",
                Detail = $"No registration request found with ID {id}"
            });
        }

        stopwatch.Stop();
        
        logger.LogInformation(
            "Registration status queried: {RegistrationId} - {Status} (Duration: {DurationMs}ms, Target: <1000ms)",
            id, request.Status, stopwatch.ElapsedMilliseconds);
        
        if (stopwatch.ElapsedMilliseconds >= 1000)
        {
            logger.LogWarning(
                "Registration status query performance degraded: {DurationMs}ms exceeded 1000ms target for {RegistrationId}",
                stopwatch.ElapsedMilliseconds, id);
        }

        return Ok(new RegistrationStatusResponseDto
        {
            RegistrationId = request.RegistrationId,
            ServiceName = request.ServiceName,
            Status = request.Status.ToString(),
            ServiceId = request.ServiceId,
            ReviewedBy = request.ReviewedBy,
            ReviewedAt = request.ReviewedAt,
            ReviewComments = request.ReviewComments,
            CreatedAt = request.CreatedAt
        });
    }

    /// <summary>
    /// Get service status and details by ID (R18)
    /// </summary>
    /// <param name="id">Service ID</param>
    /// <returns>Service status details</returns>
    [HttpGet("service/{id}")]
    [ProducesResponseType(typeof(ServiceStatusResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceStatusResponseDto>> GetServiceStatus(Guid id)
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        Service? service = await catalogService.GetServiceByIdAsync(id);

        if (service == null)
        {
            stopwatch.Stop();
            logger.LogWarning("Service status query failed: {ServiceId} not found (Duration: {DurationMs}ms)", 
                id, stopwatch.ElapsedMilliseconds);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Service Not Found",
                Detail = $"No service found with ID {id}"
            });
        }

        stopwatch.Stop();
        
        logger.LogInformation(
            "Service status queried: {ServiceId} - {ServiceName} - {HealthStatus} (Duration: {DurationMs}ms, Target: <1000ms)",
            id, service.ServiceName, service.HealthStatus, stopwatch.ElapsedMilliseconds);
        
        if (stopwatch.ElapsedMilliseconds >= 1000)
        {
            logger.LogWarning(
                "Service status query performance degraded: {DurationMs}ms exceeded 1000ms target for {ServiceId}",
                stopwatch.ElapsedMilliseconds, id);
        }

        return Ok(new ServiceStatusResponseDto
        {
            ServiceId = service.ServiceId,
            ServiceName = service.ServiceName,
            Description = service.Description,
            ContactEmail = service.ContactEmail,
            Endpoints = JsonSerializer.Deserialize<List<string>>(service.Endpoints) ?? new(),
            HeartbeatTimeout = service.HeartbeatTimeout,
            MaxMissedHeartbeats = service.MaxMissedHeartbeats,
            HealthStatus = service.HealthStatus.ToString(),
            MissedHeartbeatCounter = service.MissedHeartbeatCounter,
            HeartbeatCount = service.HeartbeatCount,
            LastHeartbeatTimestamp = service.LastHeartbeatTimestamp,
            CreatedAt = service.CreatedAt,
            UpdatedAt = service.UpdatedAt
        });
    }
}
