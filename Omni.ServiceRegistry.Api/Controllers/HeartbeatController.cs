using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.RateLimiting;
using Omni.ServiceRegistry.Api.DTOs;
using Omni.ServiceRegistry.Interfaces;

namespace Omni.ServiceRegistry.Api.Controllers;

/// <summary>
/// Controller for heartbeat endpoints
/// </summary>
[ApiController]
[Route("api/v1/heartbeat")]
public class HeartbeatController : ControllerBase
{
    private readonly IHeartbeatService heartbeatService;
    private readonly IServiceCatalogService catalogService;
    private readonly ILogger<HeartbeatController> logger;
    
    public HeartbeatController(
        IHeartbeatService heartbeatService,
        IServiceCatalogService catalogService,
        ILogger<HeartbeatController> logger)
    {
        this.heartbeatService = heartbeatService ?? throw new ArgumentNullException(nameof(heartbeatService));
        this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Submit a heartbeat signal for a registered service
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service</param>
    /// <param name="request">Optional heartbeat metadata</param>
    /// <returns>Acknowledgment with timestamp and current health status</returns>
    /// <response code="200">Heartbeat received and processed successfully</response>
    /// <response code="403">Service is not approved or is deleted</response>
    /// <response code="404">Service not found</response>
    /// <response code="429">Rate limit exceeded</response>
    [HttpPost("{serviceId}")]
    [EnableRateLimiting("heartbeat")]
    [ProducesResponseType(typeof(HeartbeatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SubmitHeartbeat(
        Guid serviceId,
        [FromBody] HeartbeatRequestDto? request)
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        logger.LogInformation("Received heartbeat for service {ServiceId}", serviceId);
        
        try
        {
            // Process heartbeat (ACK immediately, process async)
            DateTime timestamp = await heartbeatService.ProcessHeartbeatAsync(
                serviceId, 
                request?.Metadata,
                logger);
            
            stopwatch.Stop();
            
            logger.LogInformation(
                "Heartbeat processed for service {ServiceId} (Duration: {DurationMs}ms, Target: <500ms)",
                serviceId, stopwatch.ElapsedMilliseconds);
            
            if (stopwatch.ElapsedMilliseconds >= 500)
            {
                logger.LogWarning(
                    "Heartbeat performance degraded: {DurationMs}ms exceeded 500ms target for service {ServiceId}",
                    stopwatch.ElapsedMilliseconds, serviceId);
            }
            
            // Retrieve current service status to include in response
            Models.Service? service = await catalogService.GetServiceByIdAsync(serviceId);
            
            if (service == null)
            {
                logger.LogWarning("Service {ServiceId} not found after heartbeat processing", serviceId);
                return NotFound(new { error = $"Service {serviceId} not found" });
            }
            
            HeartbeatResponseDto response = new HeartbeatResponseDto
            {
                Timestamp = timestamp,
                Status = service.HealthStatus.ToString(),
                Message = "Heartbeat acknowledged"
            };
            
            logger.LogTrace("Heartbeat acknowledged for service {ServiceId} at {Timestamp}", serviceId, timestamp);
            
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // Service not found or not active
            if (ex.Message.Contains("not found"))
            {
                logger.LogWarning("Heartbeat rejected for service {ServiceId}: {Error}", serviceId, ex.Message);
                return NotFound(new { error = ex.Message });
            }
            else if (ex.Message.Contains("not active"))
            {
                logger.LogWarning("Heartbeat rejected for service {ServiceId}: {Error}", serviceId, ex.Message);
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            
            logger.LogError(ex, "Error processing heartbeat for service {ServiceId}", serviceId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing heartbeat for service {ServiceId}", serviceId);
            throw;
        }
    }
}
