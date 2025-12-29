using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Api.DTOs;

/// <summary>
/// Response DTO for detailed service status query (R18)
/// </summary>
public class ServiceStatusResponseDto
{
    /// <summary>
    /// Service ID
    /// </summary>
    public Guid ServiceId { get; set; }
    
    /// <summary>
    /// Service name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Service description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Contact email
    /// </summary>
    public string ContactEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Endpoint URLs
    /// </summary>
    public List<string> Endpoints { get; set; } = new();
    
    /// <summary>
    /// Heartbeat configuration - timeout in seconds
    /// </summary>
    public int HeartbeatTimeout { get; set; }
    
    /// <summary>
    /// Heartbeat configuration - max missed heartbeats
    /// </summary>
    public int MaxMissedHeartbeats { get; set; }
    
    /// <summary>
    /// Current health status
    /// </summary>
    public string HealthStatus { get; set; } = string.Empty;
    
    /// <summary>
    /// Missed heartbeat counter
    /// </summary>
    public int MissedHeartbeatCounter { get; set; }
    
    /// <summary>
    /// Total heartbeat count
    /// </summary>
    public long HeartbeatCount { get; set; }
    
    /// <summary>
    /// Last heartbeat timestamp (null if never received)
    /// </summary>
    public DateTime? LastHeartbeatTimestamp { get; set; }
    
    /// <summary>
    /// Service creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
