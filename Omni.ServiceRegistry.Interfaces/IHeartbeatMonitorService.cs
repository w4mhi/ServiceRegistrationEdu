using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Service for monitoring heartbeat timeouts and managing health status transitions
/// </summary>
public interface IHeartbeatMonitorService
{
    /// <summary>
    /// Checks health status for a specific service based on heartbeat timeout
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service to check</param>
    /// <param name="logger">Logger instance for diagnostic output</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task CheckServiceHealthAsync(Guid serviceId, ILogger? logger = null);
    
    /// <summary>
    /// Retrieves all services that are currently unhealthy (UNHEALTHY, DEGRADED, or DEAD)
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output</param>
    /// <returns>List of services with unhealthy status</returns>
    Task<List<Models.Service>> GetUnhealthyServicesAsync(ILogger? logger = null);
}
