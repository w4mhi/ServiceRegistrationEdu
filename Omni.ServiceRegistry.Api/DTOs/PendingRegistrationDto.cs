using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Api.DTOs;

using Omni.ServiceRegistry.Models;

/// <summary>
/// Response DTO for pending registration list (R25)
/// </summary>
public class PendingRegistrationDto
{
    /// <summary>
    /// Registration request ID
    /// </summary>
    public Guid RegistrationId { get; set; }
    
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
    /// Heartbeat timeout in seconds
    /// </summary>
    public int HeartbeatTimeout { get; set; }
    
    /// <summary>
    /// Max missed heartbeats
    /// </summary>
    public int MaxMissedHeartbeats { get; set; }
    
    /// <summary>
    /// Submission timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Registration status (Pending, Approved, Denied)
    /// </summary>
    public RegistrationStatus Status { get; set; }
    
    /// <summary>
    /// Reviewer comments (approval or denial reason)
    /// </summary>
    public string? ReviewComments { get; set; }
    
    /// <summary>
    /// Review timestamp
    /// </summary>
    public DateTime? ReviewedAt { get; set; }
}
