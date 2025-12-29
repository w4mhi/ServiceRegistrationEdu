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
/// Response DTO for registration submission (R8-R10)
/// </summary>
public class RegistrationResponseDto
{
    /// <summary>
    /// Unique registration request identifier
    /// </summary>
    public Guid RegistrationId { get; set; }
    
    /// <summary>
    /// Current registration status (pending/approved/denied)
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable status message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Service ID (only populated after approval)
    /// </summary>
    public Guid? ServiceId { get; set; }
}
