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
/// Response DTO for registration status queries (R9)
/// </summary>
public class RegistrationStatusResponseDto
{
    /// <summary>
    /// Registration request identifier
    /// </summary>
    public Guid RegistrationId { get; set; }
    
    /// <summary>
    /// Service name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Current registration status
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Service ID (if approved)
    /// </summary>
    public Guid? ServiceId { get; set; }
    
    /// <summary>
    /// Administrator who reviewed (if reviewed)
    /// </summary>
    public string? ReviewedBy { get; set; }
    
    /// <summary>
    /// Review timestamp (if reviewed)
    /// </summary>
    public DateTime? ReviewedAt { get; set; }
    
    /// <summary>
    /// Review comments (if denied)
    /// </summary>
    public string? ReviewComments { get; set; }
    
    /// <summary>
    /// Submission timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
