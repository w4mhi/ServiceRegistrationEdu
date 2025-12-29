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
/// Request payload for heartbeat submission
/// </summary>
public class HeartbeatRequestDto
{
    /// <summary>
    /// Optional metadata from the service sending the heartbeat
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
