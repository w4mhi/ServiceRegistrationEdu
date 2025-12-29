using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Models;

/// <summary>
/// Pending service registration submission (R1-R7)
/// </summary>
public class RegistrationRequest
{
    /// <summary>
    /// Unique registration request identifier
    /// </summary>
    public Guid RegistrationId { get; set; }
    
    /// <summary>
    /// Service name (3-50 chars, alphanumeric + hyphen)
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// SHA256 hash of lowercase service name for uniqueness validation
    /// </summary>
    public string ServiceNameNormalized { get; set; } = string.Empty;
    
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
    public string Endpoints { get; set; } = "[]";
    
    /// <summary>
    /// Heartbeat timeout in seconds (R33)
    /// </summary>
    public int HeartbeatTimeout { get; set; }
    
    /// <summary>
    /// Maximum allowed missed heartbeats before DEAD status (R34)
    /// </summary>
    public int MaxMissedHeartbeats { get; set; }
    
    /// <summary>
    /// Current registration status
    /// </summary>
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;
    
    /// <summary>
    /// Timestamp when registration was submitted
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Timestamp when registration was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Administrator who approved/denied the registration
    /// </summary>
    public string? ReviewedBy { get; set; }
    
    /// <summary>
    /// Timestamp when registration was reviewed
    /// </summary>
    public DateTime? ReviewedAt { get; set; }
    
    /// <summary>
    /// Administrator comments (required for denial, R7)
    /// </summary>
    public string? ReviewComments { get; set; }
    
    /// <summary>
    /// Service ID assigned upon approval
    /// </summary>
    public Guid? ServiceId { get; set; }
}
