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
/// Administrator endpoints for approval workflow (US2, R11-R13, R25-R27)
/// </summary>
[ApiController]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdministratorService administratorService;
    private readonly ILogger<AdminController> logger;

    public AdminController(
        IAdministratorService administratorService,
        ILogger<AdminController> logger)
    {
        this.administratorService = administratorService;
        this.logger = logger;
    }

    /// <summary>
    /// Get all pending registration requests (R25)
    /// </summary>
    [HttpGet("registrations/pending")]
    [ProducesResponseType(typeof(List<PendingRegistrationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PendingRegistrationDto>>> GetPendingRegistrations()
    {
        List<RegistrationRequest> pending = await administratorService.GetPendingRegistrationsAsync();

        List<PendingRegistrationDto> dtos = pending.Select(r => new PendingRegistrationDto
        {
            RegistrationId = r.RegistrationId,
            ServiceName = r.ServiceName,
            Description = r.Description,
            ContactEmail = r.ContactEmail,
            Endpoints = JsonSerializer.Deserialize<List<string>>(r.Endpoints) ?? new(),
            HeartbeatTimeout = r.HeartbeatTimeout,
            MaxMissedHeartbeats = r.MaxMissedHeartbeats,
            CreatedAt = r.CreatedAt
        }).ToList();

        logger.LogInformation("Returned {Count} pending registrations", dtos.Count);

        return Ok(dtos);
    }
    
    /// <summary>
    /// Get all registration requests (pending and denied) for management UI
    /// </summary>
    [HttpGet("registrations/all")]
    [ProducesResponseType(typeof(List<PendingRegistrationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PendingRegistrationDto>>> GetAllRegistrations()
    {
        List<RegistrationRequest> all = await administratorService.GetAllRegistrationsAsync();

        List<PendingRegistrationDto> dtos = all.Select(r => new PendingRegistrationDto
        {
            RegistrationId = r.RegistrationId,
            ServiceName = r.ServiceName,
            Description = r.Description,
            ContactEmail = r.ContactEmail,
            Endpoints = JsonSerializer.Deserialize<List<string>>(r.Endpoints) ?? new(),
            HeartbeatTimeout = r.HeartbeatTimeout,
            MaxMissedHeartbeats = r.MaxMissedHeartbeats,
            CreatedAt = r.CreatedAt,
            Status = r.Status,
            ReviewComments = r.ReviewComments,
            ReviewedAt = r.ReviewedAt
        }).ToList();

        logger.LogInformation("Returned {Count} total registrations (pending + denied)", dtos.Count);

        return Ok(dtos);
    }

    /// <summary>
    /// Approve a registration request (R11-R12)
    /// </summary>
    [HttpPost("registrations/{id}/approve")]
    [ProducesResponseType(typeof(RegistrationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RegistrationResponseDto>> ApproveRegistration(
        Guid id,
        [FromBody] ApprovalRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        try
        {
            Service service = await administratorService.ApproveRegistrationAsync(
                id, request.ApprovedBy, request.Comments);

            logger.LogInformation(
                "Registration {RegistrationId} approved, service {ServiceId} created",
                id, service.ServiceId);

            return Ok(new RegistrationResponseDto
            {
                RegistrationId = id,
                Status = "Approved",
                Message = "Registration approved and service created successfully.",
                ServiceId = service.ServiceId
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to approve registration {RegistrationId}", id);
            
            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Registration Not Found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Approval Failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Deny a registration request (R12-R13)
    /// </summary>
    [HttpPost("registrations/{id}/deny")]
    [ProducesResponseType(typeof(RegistrationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RegistrationResponseDto>> DenyRegistration(
        Guid id,
        [FromBody] DenialRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        try
        {
            await administratorService.DenyRegistrationAsync(id, request.DeniedBy, request.Comments);

            logger.LogInformation(
                "Registration {RegistrationId} denied by {DeniedBy}",
                id, request.DeniedBy);

            return Ok(new RegistrationResponseDto
            {
                RegistrationId = id,
                Status = "Denied",
                Message = "Registration has been denied.",
                ServiceId = null
            });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid denial request for {RegistrationId}", id);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to deny registration {RegistrationId}", id);
            
            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Registration Not Found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Denial Failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get all pending deletion requests
    /// </summary>
    [HttpGet("deletions/pending")]
    [ProducesResponseType(typeof(List<ServiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ServiceDto>>> GetPendingDeletions()
    {
        List<Service> pending = await administratorService.GetPendingDeletionsAsync();

        List<ServiceDto> dtos = pending.Select(s => new ServiceDto
        {
            ServiceId = s.ServiceId,
            ServiceName = s.ServiceName,
            Description = s.Description,
            ContactEmail = s.ContactEmail,
            Endpoints = JsonSerializer.Deserialize<List<string>>(s.Endpoints) ?? new(),
            HealthStatus = s.HealthStatus.ToString(),
            LastHeartbeatTimestamp = s.LastHeartbeatTimestamp,
            HeartbeatTimeout = s.HeartbeatTimeout,
            MaxMissedHeartbeats = s.MaxMissedHeartbeats,
            MissedHeartbeatCounter = s.MissedHeartbeatCounter,
            HeartbeatCount = s.HeartbeatCount,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();

        logger.LogInformation("Returned {Count} pending deletions", dtos.Count);

        return Ok(dtos);
    }

    /// <summary>
    /// Request service deletion
    /// </summary>
    [HttpPost("services/{id}/delete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestDeletion(
        Guid id,
        [FromBody] DeletionRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        try
        {
            await administratorService.RequestDeletionAsync(
                id, "admin", request.Reason, request.Comments);

            logger.LogInformation("Deletion requested for service {ServiceId}", id);

            return Ok(new { message = "Deletion request submitted successfully." });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid deletion request for {ServiceId}", id);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to request deletion for {ServiceId}", id);
            
            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Service Not Found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Deletion Request Failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Approve a deletion request
    /// </summary>
    [HttpPost("deletions/{id}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveDeletion(Guid id)
    {
        try
        {
            await administratorService.ApproveDeletionAsync(id, "admin");

            logger.LogInformation("Deletion approved for service {ServiceId}", id);

            return Ok(new { message = "Service deletion approved successfully." });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to approve deletion for {ServiceId}", id);
            
            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Service Not Found",
                    Detail = ex.Message
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Deletion Approval Failed",
                Detail = ex.Message
            });
        }
    }
}
