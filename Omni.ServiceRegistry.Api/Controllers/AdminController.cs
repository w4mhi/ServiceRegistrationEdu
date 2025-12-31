using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly IServiceCatalogService catalogService;
    private readonly IDeletionCycleRepository deletionCycleRepository;
    private readonly ILogger<AdminController> logger;

    public AdminController(
        IAdministratorService administratorService,
        IServiceCatalogService catalogService,
        IDeletionCycleRepository deletionCycleRepository,
        ILogger<AdminController> logger)
    {
        this.administratorService = administratorService;
        this.catalogService = catalogService;
        this.deletionCycleRepository = deletionCycleRepository;
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
    /// Approve multiple registration requests at once (bulk approval)
    /// </summary>
    [HttpPost("registrations/approve-all")]
    [ProducesResponseType(typeof(BulkApprovalResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkApprovalResponseDto>> ApproveAllRegistrations(
        [FromBody] BulkApprovalRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        if (request.RegistrationIds == null || request.RegistrationIds.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = "No registration IDs provided"
            });
        }
        
        List<Guid> successfulApprovals = new List<Guid>();
        List<(Guid RegistrationId, string Error)> failures = new List<(Guid, string)>();
        
        foreach (Guid registrationId in request.RegistrationIds)
        {
            try
            {
                Service service = await administratorService.ApproveRegistrationAsync(
                    registrationId, request.ApprovedBy, request.Comments);
                
                successfulApprovals.Add(registrationId);
                
                logger.LogInformation(
                    "Registration {RegistrationId} approved (bulk), service {ServiceId} created",
                    registrationId, service.ServiceId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to approve registration {RegistrationId} in bulk operation", registrationId);
                failures.Add((registrationId, ex.Message));
            }
        }
        
        logger.LogInformation(
            "Bulk approval completed: {SuccessCount} successful, {FailureCount} failed",
            successfulApprovals.Count, failures.Count);
        
        return Ok(new BulkApprovalResponseDto
        {
            SuccessfulApprovals = successfulApprovals,
            Failures = failures.Select(f => new BulkApprovalFailureDto
            {
                RegistrationId = f.RegistrationId,
                ErrorMessage = f.Error
            }).ToList(),
            TotalProcessed = request.RegistrationIds.Count,
            SuccessCount = successfulApprovals.Count,
            FailureCount = failures.Count
        });
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
            UpdatedAt = s.UpdatedAt,
            DeletionStatus = s.DeletionStatus
        }).ToList();

        logger.LogInformation("Returned {Count} pending deletions", dtos.Count);

        return Ok(dtos);
    }

    /// <summary>
    /// Get all deleted services (history)
    /// </summary>
    [HttpGet("deletions/history")]
    [ProducesResponseType(typeof(List<ServiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ServiceDto>>> GetDeletedServices()
    {
        List<Service> deleted = await catalogService.GetServicesByDeletionStatusAsync(DeletionStatus.Deleted);

        List<ServiceDto> dtos = deleted.Select(s => new ServiceDto
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
            UpdatedAt = s.UpdatedAt,
            DeletionStatus = s.DeletionStatus,
            DeletionReason = s.DeletionReason,
            DeletionApprovedBy = s.DeletionApprovedBy,
            DeletionApprovedAt = s.DeletionApprovedAt
        }).ToList();

        logger.LogInformation("Returned {Count} deleted services", dtos.Count);

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
    public async Task<IActionResult> ApproveDeletion(Guid id, [FromBody] ApproveDeletionDto? request = null)
    {
        try
        {
            await administratorService.ApproveDeletionAsync(id, "admin", request?.Reason);

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
    
    /// <summary>
    /// Check if a deleted service is eligible for restoration
    /// </summary>
    [HttpGet("services/{id}/restoration-eligibility")]
    [ProducesResponseType(typeof(RestorationEligibilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestorationEligibilityDto>> GetRestorationEligibility(Guid id)
    {
        try
        {
            (bool isEligible, string? reason, string? restorationType, int? daysUntilExpiration) = 
                await administratorService.CheckRestorationEligibilityAsync(id);
            
            RestorationEligibilityDto dto = new RestorationEligibilityDto
            {
                IsEligible = isEligible,
                Reason = reason,
                RestorationType = restorationType,
                DaysUntilExpiration = daysUntilExpiration
            };
            
            return Ok(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking restoration eligibility for service {ServiceId}", id);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Service Not Found",
                Detail = ex.Message
            });
        }
    }
    
    /// <summary>
    /// Restore a recently deleted service (Quick Restore - within 7 days)
    /// </summary>
    [HttpPost("services/{id}/quick-restore")]
    [EnableRateLimiting("restoration")]
    // TODO: Add [Authorize(Roles = "Admin")] when authentication is implemented
    [ProducesResponseType(typeof(ServiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceDto>> QuickRestoreService(Guid id, [FromBody] RestoreServiceRequestDto request)
    {
        // TODO: Validate admin authorization when authentication is implemented
        // Ensure user has Admin role and permissions to restore services
        
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 10)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid Request",
                    Detail = "Restoration reason is required and must be at least 10 characters."
                });
            }
            
            Service service = await administratorService.RestoreServiceQuicklyAsync(id, request.RestoredBy, request.Reason);
            
            ServiceDto dto = new ServiceDto
            {
                ServiceId = service.ServiceId,
                ServiceName = service.ServiceName,
                Description = service.Description,
                ContactEmail = service.ContactEmail,
                Endpoints = JsonSerializer.Deserialize<List<string>>(service.Endpoints) ?? new List<string>(),
                HealthStatus = service.HealthStatus.ToString(),
                HeartbeatTimeout = service.HeartbeatTimeout,
                MaxMissedHeartbeats = service.MaxMissedHeartbeats,
                LastHeartbeatTimestamp = service.LastHeartbeatTimestamp,
                CreatedAt = service.CreatedAt,
                DeletionStatus = service.DeletionStatus,
                DeletionReason = service.DeletionReason,
                DeletionApprovedBy = service.DeletionApprovedBy,
                DeletionApprovedAt = service.DeletionApprovedAt
            };
            
            logger.LogInformation("Service {ServiceId} restored via Quick Restore by {RestoredBy}", id, request.RestoredBy);
            
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to quick restore service {ServiceId}", id);
            
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
                Title = "Quick Restore Failed",
                Detail = ex.Message
            });
        }
    }
    
    /// <summary>
    /// Restore a service deleted beyond quick restore window (Full Restore - requires heartbeat validation)
    /// </summary>
    [HttpPost("services/{id}/restore")]
    [EnableRateLimiting("restoration")]
    // TODO: Add [Authorize(Roles = "Admin")] when authentication is implemented
    // TODO: Consider requiring different admin than who deleted (separation of duties)
    [ProducesResponseType(typeof(ServiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceDto>> RestoreService(Guid id, [FromBody] RestoreServiceRequestDto request)
    {
        // TODO: Validate admin authorization when authentication is implemented
        // TODO: Verify owner authorization has been confirmed (ownerVerified flag)
        // TODO: Verify endpoints have been validated (endpointsVerified flag)
        // TODO: Log admin who performed restoration for audit trail
        
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 10)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid Request",
                    Detail = "Restoration reason is required and must be at least 10 characters."
                });
            }
            
            Service service = await administratorService.RestoreServiceFullyAsync(
                id, 
                request.RestoredBy, 
                request.Reason,
                request.Justification,
                request.OwnerVerified,
                request.EndpointsVerified);
            
            ServiceDto dto = new ServiceDto
            {
                ServiceId = service.ServiceId,
                ServiceName = service.ServiceName,
                Description = service.Description,
                ContactEmail = service.ContactEmail,
                Endpoints = JsonSerializer.Deserialize<List<string>>(service.Endpoints) ?? new List<string>(),
                HealthStatus = service.HealthStatus.ToString(),
                HeartbeatTimeout = service.HeartbeatTimeout,
                MaxMissedHeartbeats = service.MaxMissedHeartbeats,
                LastHeartbeatTimestamp = service.LastHeartbeatTimestamp,
                CreatedAt = service.CreatedAt,
                DeletionStatus = service.DeletionStatus,
                DeletionReason = service.DeletionReason,
                DeletionApprovedBy = service.DeletionApprovedBy,
                DeletionApprovedAt = service.DeletionApprovedAt
            };
            
            logger.LogInformation("Service {ServiceId} restored via Full Restore by {RestoredBy}", id, request.RestoredBy);
            
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to restore service {ServiceId}", id);
            
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
                Title = "Full Restore Failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get deletion/restoration history for a service
    /// </summary>
    [HttpGet("services/{id}/deletion-cycles")]
    [ProducesResponseType(typeof(List<ServiceDeletionCycleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ServiceDeletionCycleDto>>> GetDeletionCycles(Guid id)
    {
        logger.LogInformation("Getting deletion cycles for service {ServiceId}", id);

        List<ServiceDeletionCycle> cycles = await deletionCycleRepository.GetByServiceIdAsync(id);

        if (cycles == null || cycles.Count == 0)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "No History Found",
                Detail = $"No deletion cycles found for service {id}"
            });
        }

        List<ServiceDeletionCycleDto> dtos = cycles.Select(c => new ServiceDeletionCycleDto
        {
            ServiceDeletionCycleId = c.ServiceDeletionCycleId,
            ServiceId = c.ServiceId,
            CycleNumber = c.CycleNumber,
            DeletionRequestedAt = c.DeletionRequestedAt,
            DeletionRequestedBy = c.DeletionRequestedBy,
            DeletionReason = c.DeletionReason,
            DeletionApprovedAt = c.DeletionApprovedAt,
            DeletionApprovedBy = c.DeletionApprovedBy,
            RestorationRequestedAt = c.RestorationRequestedAt,
            RestorationRequestedBy = c.RestorationRequestedBy,
            RestorationReason = c.RestorationReason,
            RestorationApprovedAt = c.RestorationApprovedAt,
            RestorationApprovedBy = c.RestorationApprovedBy,
            RestorationMethod = c.RestorationMethod
        }).ToList();

        logger.LogInformation("Returned {Count} deletion cycles for service {ServiceId}", dtos.Count, id);
        return Ok(dtos);
    }

    /// <summary>
    /// Get recently restored services for badge display
    /// </summary>
    [HttpGet("services/recently-restored")]
    [ProducesResponseType(typeof(List<RecentlyRestoredServiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RecentlyRestoredServiceDto>>> GetRecentlyRestoredServices()
    {
        logger.LogInformation("Getting recently restored services");

        List<ServiceDeletionCycle> allCycles = await deletionCycleRepository.GetAllAsync();
        
        // Get configuration values
        int quickRestoreBadgeDays = 7; // Default from appsettings.json
        int fullRestoreBadgeDays = 14; // Default from appsettings.json
        
        DateTime now = DateTime.UtcNow;
        List<RecentlyRestoredServiceDto> recentlyRestored = new List<RecentlyRestoredServiceDto>();

        // Group by ServiceId and get latest restoration
        Dictionary<Guid, ServiceDeletionCycle> latestRestorations = allCycles
            .Where(c => c.RestorationApprovedAt.HasValue)
            .GroupBy(c => c.ServiceId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.RestorationApprovedAt).First()
            );

        foreach (KeyValuePair<Guid, ServiceDeletionCycle> kvp in latestRestorations)
        {
            ServiceDeletionCycle cycle = kvp.Value;
            if (!cycle.RestorationApprovedAt.HasValue) continue;

            TimeSpan timeSinceRestoration = now - cycle.RestorationApprovedAt.Value;
            int badgeDays = cycle.RestorationMethod == "Quick" ? quickRestoreBadgeDays : fullRestoreBadgeDays;

            if (timeSinceRestoration.TotalDays <= badgeDays)
            {
                recentlyRestored.Add(new RecentlyRestoredServiceDto
                {
                    ServiceId = cycle.ServiceId,
                    RestorationMethod = cycle.RestorationMethod ?? "Unknown",
                    RestorationApprovedAt = cycle.RestorationApprovedAt.Value,
                    DaysAgo = (int)timeSinceRestoration.TotalDays,
                    BadgeExpiresIn = badgeDays - (int)timeSinceRestoration.TotalDays
                });
            }
        }

        logger.LogInformation("Found {Count} recently restored services", recentlyRestored.Count);
        return Ok(recentlyRestored);
    }
}
