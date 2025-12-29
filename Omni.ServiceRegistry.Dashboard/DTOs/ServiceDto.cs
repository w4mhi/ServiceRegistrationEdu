using System;
using System.Collections.Generic;

namespace Omni.ServiceRegistry.Dashboard.DTOs;

/// <summary>
/// Dashboard DTO for service catalog
/// </summary>
public class ServiceDto
{
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public List<string> Endpoints { get; set; } = new();
    public string HealthStatus { get; set; } = string.Empty;
    public DateTime? LastHeartbeatTimestamp { get; set; }
    public int HeartbeatTimeout { get; set; }
    public int MaxMissedHeartbeats { get; set; }
    public int MissedHeartbeatCounter { get; set; }
    public long HeartbeatCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
