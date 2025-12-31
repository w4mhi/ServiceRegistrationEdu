using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Collections.Concurrent;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;
using Microsoft.Extensions.Logging;

namespace Omni.ServiceRegistry.Services;

/// <summary>
/// Internal state for tracking heartbeat processing in memory
/// </summary>
internal class HeartbeatState
{
    public DateTime LastHeartbeatTimestamp { get; set; }
    public HealthStatus CurrentHealthStatus { get; set; } = HealthStatus.Healthy;
    public int ConsecutiveSuccessfulHeartbeats { get; set; }
}

/// <summary>
/// Service for processing heartbeat signals from registered services
/// Updates database on every heartbeat for real-time monitoring
/// </summary>
public class HeartbeatService : IHeartbeatService
{
    private readonly IServiceRepository serviceRepository;
    private readonly ConcurrentDictionary<Guid, HeartbeatState> heartbeatStates;
    private readonly IHealthStatusNotifier? healthStatusNotifier;
    
    public HeartbeatService(
        IServiceRepository serviceRepository,
        IHealthStatusNotifier? healthStatusNotifier = null)
    {
        this.serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
        this.heartbeatStates = new ConcurrentDictionary<Guid, HeartbeatState>();
        this.healthStatusNotifier = healthStatusNotifier;
    }
    
    /// <inheritdoc/>
    public async Task<DateTime> ProcessHeartbeatAsync(Guid serviceId, Dictionary<string, string>? metadata, ILogger? logger = null)
    {
        logger?.LogTrace("Processing heartbeat for service {ServiceId}", serviceId);
        
        // Retrieve service from repository
        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        
        if (service == null)
        {
            logger?.LogWarning("Heartbeat received for non-existent service {ServiceId}", serviceId);
            throw new InvalidOperationException($"Service {serviceId} not found");
        }
        
        // Allow deleted services to send heartbeats (for restoration eligibility)
        // Only block PendingDeletion services
        if (service.DeletionStatus == DeletionStatus.PendingDeletion)
        {
            logger?.LogWarning("Heartbeat received for pending deletion service {ServiceId}", serviceId);
            throw new InvalidOperationException($"Service {serviceId} is pending deletion and cannot send heartbeats");
        }
        
        // Get or create in-memory state for health status calculation
        HeartbeatState state = heartbeatStates.GetOrAdd(serviceId, _ => new HeartbeatState
        {
            CurrentHealthStatus = service.HealthStatus,
            LastHeartbeatTimestamp = service.LastHeartbeatTimestamp ?? DateTime.UtcNow
        });
        
        // Update timestamp
        DateTime currentTimestamp = DateTime.UtcNow;
        state.LastHeartbeatTimestamp = currentTimestamp;
        
        // Calculate new health status (heartbeat received = HEALTHY or RECOVERED)
        HealthStatus previousStatus = state.CurrentHealthStatus;
        HealthStatus newStatus = CalculateHealthStatusOnHeartbeat(previousStatus, service.MaxMissedHeartbeats, state);
        state.CurrentHealthStatus = newStatus;
        
        // Log status changes
        if (newStatus != previousStatus)
        {
            if (IsRecoveryTransition(previousStatus, newStatus))
            {
                logger?.LogWarning(
                    "Service {ServiceId} recovering: {PreviousStatus} → {NewStatus} (Consecutive heartbeats: {ConsecutiveCount})",
                    serviceId, previousStatus, newStatus, state.ConsecutiveSuccessfulHeartbeats);
            }
            else
            {
                logger?.LogInformation(
                    "Service {ServiceId} health status changed from {PreviousStatus} to {NewStatus}",
                    serviceId, previousStatus, newStatus);
            }
            
            // Broadcast status change via SignalR
            if (healthStatusNotifier != null)
            {
                await healthStatusNotifier.NotifyHealthStatusChangedAsync(
                    serviceId, 
                    service.ServiceName,
                    previousStatus.ToString(), 
                    newStatus.ToString(),
                    logger);
            }
        }
        
        // Update service in database (write every heartbeat)
        service.HealthStatus = newStatus;
        service.LastHeartbeatTimestamp = currentTimestamp;
        service.HeartbeatCount++;
        service.MissedHeartbeatCounter = 0; // Reset missed counter on heartbeat
        
        // Track consecutive healthy heartbeats for deleted services (restoration eligibility)
        if (service.DeletionStatus == DeletionStatus.Deleted)
        {
            service.ConsecutiveHealthyHeartbeats++;
            logger?.LogInformation(
                "Deleted service {ServiceId} heartbeat tracked: {Count} consecutive healthy heartbeats",
                serviceId, service.ConsecutiveHealthyHeartbeats);
        }
        else
        {
            // Active services don't need this counter
            service.ConsecutiveHealthyHeartbeats = 0;
        }
        
        await serviceRepository.UpdateAsync(service);
        
        logger?.LogInformation(
            "Heartbeat processed for service {ServiceId} at {Timestamp}",
            serviceId, currentTimestamp);
        
        return currentTimestamp;
    }
    
    /// <summary>
    /// Calculate health status when heartbeat is received
    /// Supports recovery transitions with consecutive heartbeat validation
    /// </summary>
    private HealthStatus CalculateHealthStatusOnHeartbeat(
        HealthStatus currentStatus, 
        int maxMissedHeartbeats, 
        HeartbeatState state)
    {
        switch (currentStatus)
        {
            case HealthStatus.Healthy:
                // Already healthy, stay healthy
                state.ConsecutiveSuccessfulHeartbeats++;
                return HealthStatus.Healthy;
                
            case HealthStatus.Unhealthy:
                // UNHEALTHY can recover directly to HEALTHY (minor degradation)
                state.ConsecutiveSuccessfulHeartbeats = 1;
                return HealthStatus.Healthy;
                
            case HealthStatus.Degraded:
                // DEGRADED recovers directly to HEALTHY per R45 (service responded within timeout)
                state.ConsecutiveSuccessfulHeartbeats = 1;
                return HealthStatus.Healthy;
                
            case HealthStatus.Dead:
                // DEAD services require RECOVERED state per R43-R44 (more severe degradation)
                state.ConsecutiveSuccessfulHeartbeats = 1;
                return HealthStatus.Recovered;
                
            case HealthStatus.Recovered:
                // RECOVERED requires consecutive successful heartbeats >= maxMissedHeartbeats per R44
                state.ConsecutiveSuccessfulHeartbeats++;
                
                if (state.ConsecutiveSuccessfulHeartbeats >= maxMissedHeartbeats)
                {
                    // Service proven stable, transition to HEALTHY
                    state.ConsecutiveSuccessfulHeartbeats = 0;
                    return HealthStatus.Healthy;
                }
                
                // Still in recovery, need more consecutive heartbeats
                return HealthStatus.Recovered;
                
            default:
                // Unknown status, default to healthy
                state.ConsecutiveSuccessfulHeartbeats = 1;
                return HealthStatus.Healthy;
        }
    }
    
    /// <summary>
    /// Determines if a status transition represents a recovery event
    /// </summary>
    private bool IsRecoveryTransition(HealthStatus previousStatus, HealthStatus newStatus)
    {
        return (previousStatus == HealthStatus.Unhealthy || 
                previousStatus == HealthStatus.Degraded || 
                previousStatus == HealthStatus.Dead) 
                && (newStatus == HealthStatus.Recovered || newStatus == HealthStatus.Healthy)
                || (previousStatus == HealthStatus.Recovered && newStatus == HealthStatus.Healthy);
    }
}
