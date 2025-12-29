using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
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
    private readonly IHealthStatusNotifier? healthStatusNotifier;
    private readonly ILogger<AdministratorService>? logger;

    public AdministratorService(
        IRegistrationRepository registrationRepository,
        IServiceRepository serviceRepository,
        IHealthStatusNotifier? healthStatusNotifier = null,
        ILogger<AdministratorService>? logger = null)
    {
        this.registrationRepository = registrationRepository;
        this.serviceRepository = serviceRepository;
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
    public async Task ApproveDeletionAsync(Guid serviceId, string approvedBy)
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

        service.DeletionStatus = DeletionStatus.Deleted;
        service.DeletionApprovedBy = approvedBy;
        service.DeletionApprovedAt = DateTime.UtcNow;
        service.UpdatedAt = DateTime.UtcNow;

        await serviceRepository.UpdateAsync(service);

        logger?.LogInformation(
            "Deletion approved for service {ServiceId} ({ServiceName}) by {ApprovedBy}",
            serviceId, service.ServiceName, approvedBy);
    }
}
