using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Api.DTOs;

/// <summary>
/// Response DTO for service catalog list (R14-R17)
/// </summary>
public class ServiceDto
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
    /// Current health status
    /// </summary>
    public string HealthStatus { get; set; } = string.Empty;
    
    /// <summary>
    /// Last heartbeat timestamp (null if never received)
    /// </summary>
    public DateTime? LastHeartbeatTimestamp { get; set; }
    
    /// <summary>
    /// Heartbeat timeout in seconds
    /// </summary>
    public int HeartbeatTimeout { get; set; }
    
    /// <summary>
    /// Maximum allowed missed heartbeats
    /// </summary>
    public int MaxMissedHeartbeats { get; set; }
    
    /// <summary>
    /// Count of consecutive missed heartbeats
    /// </summary>
    public int MissedHeartbeatCounter { get; set; }
    
    /// <summary>
    /// Total heartbeat count
    /// </summary>
    public long HeartbeatCount { get; set; }
    
    /// <summary>
    /// Service creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Service last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Service deletion status
    /// </summary>
    public DeletionStatus DeletionStatus { get; set; } = DeletionStatus.Active;
    
    /// <summary>
    /// Reason for deletion request
    /// </summary>
    public string? DeletionReason { get; set; }
    
    /// <summary>
    /// Administrator who approved deletion
    /// </summary>
    public string? DeletionApprovedBy { get; set; }
    
    /// <summary>
    /// Timestamp when deletion was approved
    /// </summary>
    public DateTime? DeletionApprovedAt { get; set; }
}
