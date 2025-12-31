using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Interface for broadcasting health status changes to connected clients via SignalR
/// </summary>
public interface IHealthStatusNotifier
{
    /// <summary>
    /// Notify all connected clients of a health status change
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    /// <param name="serviceName">The service name</param>
    /// <param name="previousStatus">Previous health status</param>
    /// <param name="newStatus">New health status</param>
    /// <param name="logger">Optional logger</param>
    Task NotifyHealthStatusChangedAsync(
        Guid serviceId, 
        string serviceName,
        string previousStatus, 
        string newStatus,
        ILogger? logger = null);
    
    /// <summary>
    /// Notify all connected clients of a new service added to catalog
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    /// <param name="serviceName">The service name</param>
    /// <param name="logger">Optional logger</param>
    Task NotifyServiceAddedAsync(Guid serviceId, string serviceName, ILogger? logger = null);
    
    /// <summary>
    /// Notify all connected clients of a service removed from catalog
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    /// <param name="serviceName">The service name</param>
    /// <param name="logger">Optional logger</param>
    Task NotifyServiceRemovedAsync(Guid serviceId, string serviceName, ILogger? logger = null);
    
    /// <summary>
    /// Notify all connected clients of a registration approval
    /// </summary>
    /// <param name="registrationId">The registration ID</param>
    /// <param name="serviceId">The new service ID</param>
    /// <param name="logger">Optional logger</param>
    Task NotifyRegistrationApprovedAsync(Guid registrationId, Guid serviceId, ILogger? logger = null);
    
    /// <summary>
    /// Notify all connected clients of a registration denial
    /// </summary>
    /// <param name="registrationId">The registration ID</param>
    /// <param name="logger">Optional logger</param>
    Task NotifyRegistrationDeniedAsync(Guid registrationId, ILogger? logger = null);
    
    /// <summary>
    /// Notify all connected clients that analysis has started for a service
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    Task NotifyAnalysisStartedAsync(Guid serviceId);
    
    /// <summary>
    /// Notify all connected clients that analysis has completed for a service
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    /// <param name="insightId">The generated insight ID</param>
    Task NotifyAnalysisCompletedAsync(Guid serviceId, Guid insightId);
    
    /// <summary>
    /// Notify all connected clients that analysis was skipped for a service
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    /// <param name="reason">Reason for skipping</param>
    Task NotifyAnalysisSkippedAsync(Guid serviceId, string reason);
    
    /// <summary>
    /// Notify all connected clients that analysis failed for a service
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    /// <param name="errorMessage">Error message</param>
    Task NotifyAnalysisFailedAsync(Guid serviceId, string errorMessage);
}
