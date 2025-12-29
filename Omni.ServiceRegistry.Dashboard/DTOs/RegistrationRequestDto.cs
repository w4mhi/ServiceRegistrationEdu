using System;
using System.Collections.Generic;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Dashboard.DTOs;

/// <summary>
/// Dashboard DTO for registration requests
/// </summary>
public class RegistrationRequestDto
{
    public Guid RegistrationId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public List<string> Endpoints { get; set; } = new();
    public int HeartbeatTimeout { get; set; }
    public int MaxMissedHeartbeats { get; set; }
    public RegistrationStatus Status { get; set; }
    public string? ReviewComments { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
