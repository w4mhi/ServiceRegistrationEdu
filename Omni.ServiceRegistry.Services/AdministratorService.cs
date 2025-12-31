using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Data.Postgres;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Services;

/// <summary>
/// Administrator service for approval workflow (US2, R11-R13, R25-R27)
/// </summary>
public class AdministratorService : IAdministratorService
{
    private readonly IRegistrationRepository registrationRepository;
    private readonly IServiceRepository serviceRepository;
    private readonly IDeletionCycleRepository deletionCycleRepository;
    private readonly IChangeHistoryRepository changeHistoryRepository;
    private readonly ServiceRegistryDbContext dbContext;
    private readonly IConfiguration configuration;
    private readonly IHealthStatusNotifier? healthStatusNotifier;
    private readonly ILogger<AdministratorService>? logger;

    public AdministratorService(
        IRegistrationRepository registrationRepository,
        IServiceRepository serviceRepository,
        IDeletionCycleRepository deletionCycleRepository,
        IChangeHistoryRepository changeHistoryRepository,
        ServiceRegistryDbContext dbContext,
        IConfiguration configuration,
        IHealthStatusNotifier? healthStatusNotifier = null,
        ILogger<AdministratorService>? logger = null)
    {
        this.registrationRepository = registrationRepository;
        this.serviceRepository = serviceRepository;
        this.deletionCycleRepository = deletionCycleRepository;
        this.changeHistoryRepository = changeHistoryRepository;
        this.dbContext = dbContext;
        this.configuration = configuration;
        this.healthStatusNotifier = healthStatusNotifier;
        this.logger = logger;
    }

    /// <summary>
    /// Get all pending registration requests (R25)
    /// </summary>
    public async Task<List<RegistrationRequest>> GetPendingRegistrationsAsync()
    {
        List<RegistrationRequest> all = await registrationRepository.GetPendingAsync();
        List<RegistrationRequest> pending = all.Where(r => r.Status == RegistrationStatus.Pending).ToList();
        
        logger?.LogInformation("Retrieved {Count} pending registration requests", pending.Count);
        
        return pending;
    }
    
    /// <summary>
    /// Get all registration requests (pending and denied) for management UI
    /// </summary>
    public async Task<List<RegistrationRequest>> GetAllRegistrationsAsync()
    {
        List<RegistrationRequest> all = await registrationRepository.GetPendingAsync();
        
        logger?.LogInformation("Retrieved {Count} total registration requests (pending + denied)", all.Count);
        
        return all;
    }

    /// <summary>
    /// Approve a registration request and create service (R11-R12)
    /// </summary>
    public async Task<Service> ApproveRegistrationAsync(Guid registrationId, string approvedBy, string? comments = null)
    {
        // Get registration request
        RegistrationRequest? request = await registrationRepository.GetByIdAsync(registrationId);
        if (request == null)
        {
            throw new InvalidOperationException($"Registration request {registrationId} not found");
        }

        if (request.Status != RegistrationStatus.Pending && request.Status != RegistrationStatus.Denied)
        {
            throw new InvalidOperationException($"Registration {registrationId} cannot be approved (current status: {request.Status})");
        }

        // Check for duplicate service name
        bool exists = await serviceRepository.ExistsByNormalizedNameAsync(request.ServiceNameNormalized);
        if (exists)
        {
            throw new InvalidOperationException($"Service with name '{request.ServiceName}' already exists");
        }

        // Update registration status
        request.Status = RegistrationStatus.Approved;
        request.ReviewedBy = approvedBy;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewComments = comments;
        request.ServiceId = Guid.NewGuid();

        await registrationRepository.UpdateAsync(request);

        // Create service entity
        Service service = new()
        {
            ServiceId = request.ServiceId.Value,
            RegistrationId = request.RegistrationId,
            ServiceName = request.ServiceName,
            ServiceNameNormalized = request.ServiceNameNormalized,
            Description = request.Description,
            ContactEmail = request.ContactEmail,
            Endpoints = request.Endpoints,
            HeartbeatTimeout = request.HeartbeatTimeout,
            MaxMissedHeartbeats = request.MaxMissedHeartbeats,
            HealthStatus = HealthStatus.Healthy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastHeartbeatTimestamp = null,
            MissedHeartbeatCounter = 0,
            HeartbeatCount = 0,
            DeletionStatus = DeletionStatus.Active
        };

        Service created = await serviceRepository.AddAsync(service);

        logger?.LogInformation(
            "Registration {RegistrationId} approved by {ApprovedBy}, service {ServiceId} created for {ServiceName}",
            registrationId, approvedBy, created.ServiceId, created.ServiceName);
        
        // Broadcast service added via SignalR
        if (healthStatusNotifier != null)
        {
            await healthStatusNotifier.NotifyServiceAddedAsync(
                created.ServiceId,
                created.ServiceName,
                logger);
            
            await healthStatusNotifier.NotifyRegistrationApprovedAsync(
                registrationId,
                created.ServiceId,
                logger);
        }

        return created;
    }

    /// <summary>
    /// Deny a registration request (R12-R13)
    /// </summary>
    public async Task DenyRegistrationAsync(Guid registrationId, string deniedBy, string comments)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            throw new ArgumentException("Comments are required when denying a registration", nameof(comments));
        }

        // Get registration request
        RegistrationRequest? request = await registrationRepository.GetByIdAsync(registrationId);
        if (request == null)
        {
            throw new InvalidOperationException($"Registration request {registrationId} not found");
        }

        if (request.Status != RegistrationStatus.Pending)
        {
            throw new InvalidOperationException($"Registration {registrationId} is not pending (current status: {request.Status})");
        }

        // Update registration status
        request.Status = RegistrationStatus.Denied;
        request.ReviewedBy = deniedBy;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewComments = comments;

        await registrationRepository.UpdateAsync(request);

        logger?.LogInformation(
            "Registration {RegistrationId} denied by {DeniedBy} for service {ServiceName}: {Comments}",
            registrationId, deniedBy, request.ServiceName, comments);
        
        // Broadcast registration denied via SignalR
        if (healthStatusNotifier != null)
        {
            await healthStatusNotifier.NotifyRegistrationDeniedAsync(registrationId, logger);
        }
    }

    /// <summary>
    /// Request service deletion (soft delete)
    /// </summary>
    public async Task RequestDeletionAsync(Guid serviceId, string requestedBy, string reason, string? comments = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason is required when requesting deletion", nameof(reason));
        }

        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        if (service == null)
        {
            throw new InvalidOperationException($"Service {serviceId} not found");
        }

        if (service.DeletionStatus != DeletionStatus.Active)
        {
            throw new InvalidOperationException($"Service {serviceId} is not active (current status: {service.DeletionStatus})");
        }

        service.DeletionStatus = DeletionStatus.PendingDeletion;
        service.DeletionRequestedBy = requestedBy;
        service.DeletionRequestedAt = DateTime.UtcNow;
        service.DeletionReason = reason;
        service.DeletionComments = comments;
        service.UpdatedAt = DateTime.UtcNow;

        await serviceRepository.UpdateAsync(service);

        logger?.LogInformation(
            "Deletion requested for service {ServiceId} ({ServiceName}) by {RequestedBy}: {Reason}",
            serviceId, service.ServiceName, requestedBy, reason);
    }

    /// <summary>
    /// Get all pending deletion requests
    /// </summary>
    public async Task<List<Service>> GetPendingDeletionsAsync()
    {
        List<Service> pending = await serviceRepository.GetByDeletionStatusAsync(DeletionStatus.PendingDeletion);
        
        logger?.LogInformation("Retrieved {Count} pending deletion requests", pending.Count);
        
        return pending;
    }

    /// <summary>
    /// Approve a deletion request (soft delete)
    /// </summary>
    public async Task ApproveDeletionAsync(Guid serviceId, string approvedBy, string? reason = null)
    {
        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        if (service == null)
        {
            throw new InvalidOperationException($"Service {serviceId} not found");
        }

        if (service.DeletionStatus != DeletionStatus.PendingDeletion)
        {
            throw new InvalidOperationException($"Service {serviceId} does not have a pending deletion request");
        }

        // Increment cycle count
        service.DeletionCycleCount++;
        
        // Create audit record for this deletion cycle
        ServiceDeletionCycle cycle = new ServiceDeletionCycle
        {
            ServiceDeletionCycleId = Guid.NewGuid(),
            ServiceId = serviceId,
            CycleNumber = service.DeletionCycleCount,
            DeletionRequestedAt = service.DeletionRequestedAt ?? DateTime.UtcNow,
            DeletionRequestedBy = service.DeletionRequestedBy ?? "unknown",
            DeletionReason = string.IsNullOrEmpty(reason) ? (service.DeletionReason ?? "No reason provided") : reason,
            DeletionApprovedAt = DateTime.UtcNow,
            DeletionApprovedBy = approvedBy
        };
        
        dbContext.ServiceDeletionCycles.Add(cycle);

        service.DeletionStatus = DeletionStatus.Deleted;
        service.DeletionApprovedBy = approvedBy;
        service.DeletionApprovedAt = DateTime.UtcNow;
        service.UpdatedAt = DateTime.UtcNow;
        
        // Update reason if provided by admin (overrides original request reason)
        if (!string.IsNullOrEmpty(reason))
        {
            service.DeletionReason = reason;
        }

        await serviceRepository.UpdateAsync(service);
        await dbContext.SaveChangesAsync();

        logger?.LogInformation(
            "Deletion approved for service {ServiceId} ({ServiceName}) by {ApprovedBy}. Deletion cycle #{CycleNumber} created.",
            serviceId, service.ServiceName, approvedBy, service.DeletionCycleCount);
    }
    
    public async Task<(bool IsEligible, string? Reason, string? RestorationType, int? DaysUntilExpiration)> CheckRestorationEligibilityAsync(Guid serviceId)
    {
        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        if (service == null)
        {
            return (false, "Service not found", null, null);
        }
        
        if (service.DeletionStatus != DeletionStatus.Deleted)
        {
            return (false, "Service is not deleted", null, null);
        }
        
        if (!service.DeletionApprovedAt.HasValue)
        {
            return (false, "Service deletion not approved", null, null);
        }
        
        int quickRestoreWindowHours = configuration.GetValue<int>("ServiceRestoration:QuickRestoreWindowHours", 168);
        TimeSpan timeSinceDeletion = DateTime.UtcNow - service.DeletionApprovedAt.Value;
        double hoursSinceDeletion = timeSinceDeletion.TotalHours;
        
        if (hoursSinceDeletion < quickRestoreWindowHours)
        {
            int hoursRemaining = (int)(quickRestoreWindowHours - hoursSinceDeletion);
            int daysRemaining = (int)Math.Ceiling(hoursRemaining / 24.0);
            return (true, "Eligible for Quick Restore", "Quick", daysRemaining);
        }
        else
        {
            int minimumHeartbeats = configuration.GetValue<int>("ServiceRestoration:MinimumConsecutiveHeartbeats", 10);
            if (service.ConsecutiveHealthyHeartbeats >= minimumHeartbeats)
            {
                return (true, "Eligible for Full Restore", "Full", null);
            }
            else
            {
                return (false, $"Insufficient healthy heartbeats. Required: {minimumHeartbeats}, Current: {service.ConsecutiveHealthyHeartbeats}", null, null);
            }
        }
    }
    
    public async Task<Service> RestoreServiceQuicklyAsync(Guid serviceId, string restoredBy, string reason)
    {
        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        if (service == null)
        {
            throw new InvalidOperationException($"Service {serviceId} not found");
        }
        
        if (service.DeletionStatus != DeletionStatus.Deleted)
        {
            throw new InvalidOperationException($"Service {serviceId} is not deleted");
        }
        
        if (!service.DeletionApprovedAt.HasValue)
        {
            throw new InvalidOperationException($"Service {serviceId} deletion not approved");
        }
        
        int quickRestoreWindowHours = configuration.GetValue<int>("ServiceRestoration:QuickRestoreWindowHours", 168);
        TimeSpan timeSinceDeletion = DateTime.UtcNow - service.DeletionApprovedAt.Value;
        
        if (timeSinceDeletion.TotalHours >= quickRestoreWindowHours)
        {
            throw new InvalidOperationException($"Service {serviceId} is beyond quick restore window. Use Full Restore instead.");
        }
        
        ServiceDeletionCycle? latestCycle = await deletionCycleRepository.GetLatestByServiceIdAsync(serviceId);
        if (latestCycle == null)
        {
            throw new InvalidOperationException($"No deletion cycle found for service {serviceId}");
        }
        
        latestCycle.RestorationRequestedAt = DateTime.UtcNow;
        latestCycle.RestorationRequestedBy = restoredBy;
        latestCycle.RestorationReason = reason;
        latestCycle.RestorationApprovedAt = DateTime.UtcNow;
        latestCycle.RestorationApprovedBy = restoredBy;
        latestCycle.RestorationMethod = "Quick";
        
        await deletionCycleRepository.UpdateAsync(latestCycle);
        
        service.DeletionStatus = DeletionStatus.Active;
        service.HealthStatus = HealthStatus.Healthy;
        service.UpdatedAt = DateTime.UtcNow;
        service.ConsecutiveHealthyHeartbeats = 0;
        
        await serviceRepository.UpdateAsync(service);
        
        // Log change history
        await changeHistoryRepository.AddAsync(new ServiceChangeHistory
        {
            ChangeId = Guid.NewGuid(),
            ServiceId = serviceId,
            ChangeType = "QuickRestored",
            ChangeDescription = $"Service quick restored by {restoredBy} after {timeSinceDeletion.TotalDays:F1} days",
            FieldName = "DeletionStatus",
            OldValue = "Deleted",
            NewValue = "Active",
            ChangedBy = restoredBy,
            ChangedAt = DateTime.UtcNow
        });
        
        logger?.LogInformation(
            "Service {ServiceId} ({ServiceName}) restored via Quick Restore by {RestoredBy}. Cycle #{CycleNumber}",
            serviceId, service.ServiceName, restoredBy, latestCycle.CycleNumber);
        
        return service;
    }
    
    public async Task<Service> RestoreServiceFullyAsync(
        Guid serviceId, 
        string restoredBy, 
        string reason,
        string? justification = null,
        bool ownerVerified = false,
        bool endpointsVerified = false)
    {
        // TODO: Validate admin performing restoration is different from admin who deleted (separation of duties)
        // TODO: Verify ownerVerified flag indicates actual owner confirmation (not just checkbox)
        // TODO: Verify endpointsVerified flag indicates actual endpoint validation (not just checkbox)
        // TODO: Consider requiring multi-factor authentication for Full Restore operations
        // TODO: Implement approval workflow: different admin must approve restoration request
        
        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        if (service == null)
        {
            throw new InvalidOperationException($"Service {serviceId} not found");
        }
        
        if (service.DeletionStatus != DeletionStatus.Deleted)
        {
            throw new InvalidOperationException($"Service {serviceId} is not deleted");
        }
        
        if (!service.DeletionApprovedAt.HasValue)
        {
            throw new InvalidOperationException($"Service {serviceId} deletion not approved");
        }
        
        // Validate inputs
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
        {
            throw new ArgumentException("Restoration reason must be at least 10 characters");
        }
        
        // For services deleted > 7 days, require justification and verifications
        int quickRestoreWindowHours = configuration.GetValue<int>("ServiceRestoration:QuickRestoreWindowHours", 168);
        TimeSpan timeSinceDeletion = DateTime.UtcNow - service.DeletionApprovedAt.Value;
        
        if (timeSinceDeletion.TotalHours >= quickRestoreWindowHours)
        {
            if (string.IsNullOrWhiteSpace(justification) || justification.Length < 50)
            {
                throw new ArgumentException("Justification must be at least 50 characters for long-term restorations");
            }
            
            if (!ownerVerified)
            {
                throw new ArgumentException("Service owner authorization must be verified for long-term restorations");
            }
            
            if (!endpointsVerified)
            {
                throw new ArgumentException("Service endpoints must be verified for long-term restorations");
            }
        }
        
        int minimumHeartbeats = configuration.GetValue<int>("ServiceRestoration:MinimumConsecutiveHeartbeats", 10);
        if (service.ConsecutiveHealthyHeartbeats < minimumHeartbeats)
        {
            throw new InvalidOperationException(
                $"Service {serviceId} does not meet heartbeat validation requirement. " +
                $"Required: {minimumHeartbeats}, Current: {service.ConsecutiveHealthyHeartbeats}");
        }
        
        ServiceDeletionCycle? latestCycle = await deletionCycleRepository.GetLatestByServiceIdAsync(serviceId);
        if (latestCycle == null)
        {
            throw new InvalidOperationException($"No deletion cycle found for service {serviceId}");
        }
        
        latestCycle.RestorationRequestedAt = DateTime.UtcNow;
        latestCycle.RestorationRequestedBy = restoredBy;
        latestCycle.RestorationReason = !string.IsNullOrEmpty(justification) 
            ? $"{reason} | Justification: {justification}" 
            : reason;
        latestCycle.RestorationApprovedAt = DateTime.UtcNow;
        latestCycle.RestorationApprovedBy = restoredBy;
        latestCycle.RestorationMethod = "Full";
        
        await deletionCycleRepository.UpdateAsync(latestCycle);
        
        service.DeletionStatus = DeletionStatus.Active;
        service.HealthStatus = HealthStatus.Healthy;
        service.UpdatedAt = DateTime.UtcNow;
        service.ConsecutiveHealthyHeartbeats = 0;
        
        await serviceRepository.UpdateAsync(service);
        
        // Log change history
        TimeSpan deletionAge = DateTime.UtcNow - service.DeletionApprovedAt.Value;
        await changeHistoryRepository.AddAsync(new ServiceChangeHistory
        {
            ChangeId = Guid.NewGuid(),
            ServiceId = serviceId,
            ChangeType = "FullRestored",
            ChangeDescription = $"Service fully restored by {restoredBy} after {deletionAge.TotalDays:F1} days with {service.ConsecutiveHealthyHeartbeats} consecutive heartbeats",
            FieldName = "DeletionStatus",
            OldValue = "Deleted",
            NewValue = "Active",
            ChangedBy = restoredBy,
            ChangedAt = DateTime.UtcNow
        });
        
        logger?.LogInformation(
            "Service {ServiceId} ({ServiceName}) restored via Full Restore by {RestoredBy}. Cycle #{CycleNumber}. Heartbeats validated: {HeartbeatCount}",
            serviceId, service.ServiceName, restoredBy, latestCycle.CycleNumber, service.ConsecutiveHealthyHeartbeats);
        
        return service;
    }
}
