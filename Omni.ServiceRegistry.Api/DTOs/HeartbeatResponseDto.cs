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
/// Response payload for heartbeat submission
/// </summary>
public class HeartbeatResponseDto
{
    /// <summary>
    /// Timestamp when the heartbeat was processed
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Current health status of the service
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Acknowledgment message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
