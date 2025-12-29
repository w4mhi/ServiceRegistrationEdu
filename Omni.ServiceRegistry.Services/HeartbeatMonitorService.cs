using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Services;

/// <summary>
/// Background service that monitors heartbeat timeouts and updates service health status
/// Runs every 5 seconds to check for services that have missed their heartbeat deadline
/// </summary>
public class HeartbeatMonitorService : BackgroundService, IHeartbeatMonitorService
{
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<HeartbeatMonitorService> logger;
    private const int monitoringIntervalSeconds = 5;
    
    public HeartbeatMonitorService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<HeartbeatMonitorService> logger)
    {
        this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Heartbeat monitoring service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (IServiceScope scope = serviceScopeFactory.CreateScope())
                {
                    IServiceRepository serviceRepository = scope.ServiceProvider.GetRequiredService<IServiceRepository>();
                    await MonitorAllServicesAsync(serviceRepository, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                throw;
            }
#pragma warning disable CA1031 // Background service must catch all exceptions to continue monitoring
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during heartbeat monitoring cycle");
            }
#pragma warning restore CA1031
            
            await Task.Delay(TimeSpan.FromSeconds(monitoringIntervalSeconds), stoppingToken);
        }
        
        logger.LogInformation("Heartbeat monitoring service stopped");
    }
    
    /// <summary>
    /// Monitors all active services for heartbeat timeouts
    /// </summary>
    private async Task MonitorAllServicesAsync(IServiceRepository serviceRepository, CancellationToken cancellationToken)
    {
        List<Service> allServices = await serviceRepository.GetAllAsync();
        
        // Filter to only active services (exclude deleted/pending deletion)
        List<Service> activeServices = allServices
            .Where(s => s.DeletionStatus == DeletionStatus.Active)
            .ToList();
        
        logger.LogTrace("Monitoring {Count} active services for heartbeat timeouts", activeServices.Count);
        
        foreach (Service service in activeServices)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            
            await CheckServiceHealthAsync(serviceRepository, service.ServiceId, logger);
        }
    }
    
    /// <inheritdoc/>
    public async Task CheckServiceHealthAsync(Guid serviceId, ILogger? logger = null)
    {
        using (IServiceScope scope = serviceScopeFactory.CreateScope())
        {
            IServiceRepository serviceRepository = scope.ServiceProvider.GetRequiredService<IServiceRepository>();
            await CheckServiceHealthAsync(serviceRepository, serviceId, logger);
        }
    }
    
    /// <summary>
    /// Internal method that checks service health with repository dependency
    /// </summary>
    private async Task CheckServiceHealthAsync(IServiceRepository serviceRepository, Guid serviceId, ILogger? logger = null)
    {
        ILogger loggerToUse = logger ?? this.logger;
        
        // Get scope to access health status notifier
        using (IServiceScope scope = serviceScopeFactory.CreateScope())
        {
            Service? service = await GetServiceOrReturnAsync(serviceRepository, serviceId, loggerToUse);
            if (service == null)
            {
                return;
            }
            
            int missedCount = CalculateMissedHeartbeats(service);
            
            if (missedCount > 0)
            {
                await HandleMissedHeartbeatsAsync(
                    serviceRepository, 
                    service, 
                    missedCount, 
                    scope, 
                    loggerToUse);
            }
        }
    }
    
    /// <summary>
    /// Retrieves service and validates it's eligible for health checking
    /// </summary>
    private async Task<Service?> GetServiceOrReturnAsync(
        IServiceRepository serviceRepository, 
        Guid serviceId, 
        ILogger loggerToUse)
    {
        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        
        if (service == null)
        {
            loggerToUse.LogWarning("Service {ServiceId} not found during health check", serviceId);
            return null;
        }
        
        if (service.DeletionStatus != DeletionStatus.Active)
        {
            return null;
        }
        
        return service;
    }
    
    /// <summary>
    /// Calculates number of missed heartbeats based on time elapsed
    /// </summary>
    private int CalculateMissedHeartbeats(Service service)
    {
        DateTime now = DateTime.UtcNow;
        DateTime lastHeartbeat = service.LastHeartbeatTimestamp ?? service.CreatedAt;
        TimeSpan timeSinceLastHeartbeat = now - lastHeartbeat;
        
        return (int)(timeSinceLastHeartbeat.TotalSeconds / service.HeartbeatTimeout);
    }
    
    /// <summary>
    /// Handles missed heartbeats by updating status and notifying
    /// </summary>
    private async Task HandleMissedHeartbeatsAsync(
        IServiceRepository serviceRepository,
        Service service,
        int missedCount,
        IServiceScope scope,
        ILogger loggerToUse)
    {
        HealthStatus previousStatus = service.HealthStatus;
        HealthStatus newStatus = CalculateHealthStatusOnMissedHeartbeat(
            previousStatus, 
            missedCount, 
            service.MaxMissedHeartbeats);
        
        if (newStatus != previousStatus || service.MissedHeartbeatCounter != missedCount)
        {
            service.HealthStatus = newStatus;
            service.MissedHeartbeatCounter = missedCount;
            await serviceRepository.UpdateAsync(service);
            
            await NotifyHealthChangeAsync(service, previousStatus, newStatus, scope, loggerToUse);
            LogHealthDegradation(service, previousStatus, newStatus, missedCount, loggerToUse);
        }
    }
    
    /// <summary>
    /// Broadcasts health status change via SignalR
    /// </summary>
    private async Task NotifyHealthChangeAsync(
        Service service,
        HealthStatus previousStatus,
        HealthStatus newStatus,
        IServiceScope scope,
        ILogger loggerToUse)
    {
        IHealthStatusNotifier? healthStatusNotifier = 
            scope.ServiceProvider.GetService<IHealthStatusNotifier>();
        
        if (healthStatusNotifier != null)
        {
            await healthStatusNotifier.NotifyHealthStatusChangedAsync(
                service.ServiceId,
                service.ServiceName,
                previousStatus.ToString(),
                newStatus.ToString(),
                loggerToUse);
        }
    }
    
    /// <summary>
    /// Logs health status degradation with appropriate severity
    /// </summary>
    private void LogHealthDegradation(
        Service service,
        HealthStatus previousStatus,
        HealthStatus newStatus,
        int missedCount,
        ILogger loggerToUse)
    {
        DateTime lastHeartbeat = service.LastHeartbeatTimestamp ?? service.CreatedAt;
        TimeSpan timeSinceLastHeartbeat = DateTime.UtcNow - lastHeartbeat;
        
        if (newStatus == HealthStatus.Dead)
        {
            loggerToUse.LogError(
                "Service {ServiceId} marked as DEAD: {PreviousStatus} → {NewStatus} " +
                "(missed {MissedCount} of {MaxMissed} heartbeats, {TimeSince:F1}s since last heartbeat)",
                service.ServiceId, previousStatus, newStatus, missedCount, 
                service.MaxMissedHeartbeats, timeSinceLastHeartbeat.TotalSeconds);
        }
        else
        {
            loggerToUse.LogWarning(
                "Service {ServiceId} health degraded: {PreviousStatus} → {NewStatus} " +
                "(missed {MissedCount} of {MaxMissed} heartbeats, {TimeSince:F1}s since last heartbeat)",
                service.ServiceId, previousStatus, newStatus, missedCount, 
                service.MaxMissedHeartbeats, timeSinceLastHeartbeat.TotalSeconds);
        }
    }
    
    /// <inheritdoc/>
    public async Task<List<Service>> GetUnhealthyServicesAsync(ILogger? logger = null)
    {
        using (IServiceScope scope = serviceScopeFactory.CreateScope())
        {
            IServiceRepository serviceRepository = scope.ServiceProvider.GetRequiredService<IServiceRepository>();
            ILogger loggerToUse = logger ?? this.logger;
            
            List<Service> allServices = await serviceRepository.GetAllAsync();
        
        List<Service> unhealthyServices = allServices
            .Where(s => s.DeletionStatus == DeletionStatus.Active)
            .Where(s => s.HealthStatus == HealthStatus.Unhealthy 
                     || s.HealthStatus == HealthStatus.Degraded 
                     || s.HealthStatus == HealthStatus.Dead)
            .ToList();
        
        loggerToUse.LogTrace(
            "Found {Count} unhealthy services (UNHEALTHY: {Unhealthy}, DEGRADED: {Degraded}, DEAD: {Dead})",
            unhealthyServices.Count,
            unhealthyServices.Count(s => s.HealthStatus == HealthStatus.Unhealthy),
            unhealthyServices.Count(s => s.HealthStatus == HealthStatus.Degraded),
            unhealthyServices.Count(s => s.HealthStatus == HealthStatus.Dead));
        
            return unhealthyServices;
        }
    }
    
    /// <summary>
    /// Calculates health status when a heartbeat is missed
    /// Transitions: HEALTHY → UNHEALTHY (1 missed) → DEGRADED (50% threshold) → DEAD (exceeded max)
    /// </summary>
    /// <param name="currentStatus">Current health status of the service</param>
    /// <param name="missedCount">Number of consecutive missed heartbeats</param>
    /// <param name="maxMissedHeartbeats">Maximum allowed missed heartbeats before DEAD</param>
    /// <returns>New health status based on missed heartbeat count</returns>
    private HealthStatus CalculateHealthStatusOnMissedHeartbeat(
        HealthStatus currentStatus, 
        int missedCount, 
        int maxMissedHeartbeats)
    {
        // Calculate DEGRADED threshold (50% of max, rounded up)
        int degradedThreshold = (int)Math.Ceiling(maxMissedHeartbeats * 0.5);
        
        // Status transitions based on missed heartbeat count
        if (missedCount > maxMissedHeartbeats)
        {
            return HealthStatus.Dead;
        }
        else if (missedCount >= degradedThreshold)
        {
            return HealthStatus.Degraded;
        }
        else if (missedCount >= 1)
        {
            return HealthStatus.Unhealthy;
        }
        else
        {
            // No missed heartbeats - maintain current status or set to HEALTHY
            return currentStatus == HealthStatus.Recovered ? HealthStatus.Healthy : currentStatus;
        }
    }
}
