using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Models;

/// <summary>
/// Health status of a registered service (R33-R45)
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Service sending heartbeats within timeout window
    /// </summary>
    Healthy = 0,
    
    /// <summary>
    /// Service missed 1 heartbeat (first timeout expired)
    /// </summary>
    Unhealthy = 1,
    
    /// <summary>
    /// Service missed 50% of max allowed heartbeats
    /// </summary>
    Degraded = 2,
    
    /// <summary>
    /// Service exceeded max missed heartbeats
    /// </summary>
    Dead = 3,
    
    /// <summary>
    /// Service recovered from DEAD with new heartbeat (R44)
    /// </summary>
    Recovered = 4,
    
    /// <summary>
    /// Service marked for deletion (soft delete)
    /// </summary>
    Deleted = 5
}
