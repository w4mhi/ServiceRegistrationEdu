using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Models;

/// <summary>
/// Approved service in the catalog (R8-R32)
/// </summary>
public class Service
{
    /// <summary>
    /// Unique service identifier (assigned at approval)
    /// </summary>
    public Guid ServiceId { get; set; }
    
    /// <summary>
    /// Original registration request ID
    /// </summary>
    public Guid RegistrationId { get; set; }
    
    /// <summary>
    /// Service name (immutable after approval)
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// SHA256 hash of lowercase service name for uniqueness
    /// </summary>
    public string ServiceNameNormalized { get; set; } = string.Empty;
    
    /// <summary>
    /// Service description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Contact email for service owner
    /// </summary>
    public string ContactEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON array of service endpoint URLs
    /// </summary>
    public string Endpoints { get; set; } = "[]";
    
    /// <summary>
    /// JSON array of API endpoints exposed by the service
    /// </summary>
    public string? ApiEndpoints { get; set; }
    
    /// <summary>
    /// Heartbeat timeout in seconds
    /// </summary>
    public int HeartbeatTimeout { get; set; }
    
    /// <summary>
    /// Maximum allowed missed heartbeats before DEAD status
    /// </summary>
    public int MaxMissedHeartbeats { get; set; }
    
    /// <summary>
    /// Current health status
    /// </summary>
    public HealthStatus HealthStatus { get; set; } = HealthStatus.Healthy;
    
    /// <summary>
    /// Timestamp when service was approved and created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Timestamp when service was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Timestamp of most recent successful heartbeat
    /// </summary>
    public DateTime? LastHeartbeatTimestamp { get; set; }
    
    /// <summary>
    /// Count of consecutive missed heartbeats (R41)
    /// </summary>
    public int MissedHeartbeatCounter { get; set; }
    
    /// <summary>
    /// Total count of heartbeats received (lifetime metric)
    /// </summary>
    public long HeartbeatCount { get; set; }
    
    /// <summary>
    /// Soft delete status (R31-R32)
    /// </summary>
    public DeletionStatus DeletionStatus { get; set; } = DeletionStatus.Active;
    
    /// <summary>
    /// Administrator who requested deletion
    /// </summary>
    public string? DeletionRequestedBy { get; set; }
    
    /// <summary>
    /// Timestamp when deletion was requested
    /// </summary>
    public DateTime? DeletionRequestedAt { get; set; }
    
    /// <summary>
    /// Reason for deletion request
    /// </summary>
    public string? DeletionReason { get; set; }
    
    /// <summary>
    /// Additional comments for deletion
    /// </summary>
    public string? DeletionComments { get; set; }
    
    /// <summary>
    /// Administrator who approved deletion
    /// </summary>
    public string? DeletionApprovedBy { get; set; }
    
    /// <summary>
    /// Timestamp when deletion was approved
    /// </summary>
    public DateTime? DeletionApprovedAt { get; set; }
    
    /// <summary>
    /// Administrator who approved deletion (legacy field)
    /// </summary>
    public string? DeletedBy { get; set; }
    
    /// <summary>
    /// Timestamp when deletion was approved
    /// </summary>
    public DateTime? DeletedAt { get; set; }
    
    /// <summary>
    /// Count of deletion/restoration cycles this service has undergone
    /// </summary>
    public int DeletionCycleCount { get; set; } = 0;
    
    /// <summary>
    /// Count of consecutive healthy heartbeats since restoration consideration
    /// </summary>
    public int ConsecutiveHealthyHeartbeats { get; set; } = 0;
    
    /// <summary>
    /// Navigation property to deletion history cycles
    /// </summary>
    public ICollection<ServiceDeletionCycle> DeletionHistory { get; set; } = new List<ServiceDeletionCycle>();
    
    /// <summary>
    /// Navigation property to AI-generated health insights
    /// </summary>
    public ICollection<ServiceHealthInsight> HealthInsights { get; set; } = new List<ServiceHealthInsight>();
    
    /// <summary>
    /// Optimistic concurrency token (EF Core)
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
