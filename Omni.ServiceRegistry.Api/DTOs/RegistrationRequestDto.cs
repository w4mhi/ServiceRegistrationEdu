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
/// Request DTO for service registration (R1-R6)
/// </summary>
public class RegistrationRequestDto
{
    /// <summary>
    /// Service name (3-50 chars, alphanumeric + hyphen)
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Brief description of service purpose
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Contact email for service owner
    /// </summary>
    public string ContactEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON array of service endpoint URLs (1-10 URLs)
    /// </summary>
    public List<string> Endpoints { get; set; } = new();
    
    /// <summary>
    /// Heartbeat timeout in seconds (recommended 15-300)
    /// </summary>
    public int HeartbeatTimeout { get; set; }
    
    /// <summary>
    /// Maximum allowed missed heartbeats (recommended 3-20)
    /// </summary>
    public int MaxMissedHeartbeats { get; set; }
}
