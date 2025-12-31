using System;

namespace Omni.ServiceRegistry.Models;

/// <summary>
/// Tracks complete deletion/restoration cycle for a service.
/// Supports multiple cycles - a service can be deleted and restored multiple times.
/// </summary>
public class ServiceDeletionCycle
{
    /// <summary>
    /// Unique identifier for this deletion cycle
    /// </summary>
    public Guid ServiceDeletionCycleId { get; set; }
    
    /// <summary>
    /// Foreign key to Service
    /// </summary>
    public Guid ServiceId { get; set; }
    
    /// <summary>
    /// Cycle number for this service (1-based, increments with each deletion)
    /// </summary>
    public int CycleNumber { get; set; }
    
    /// <summary>
    /// Timestamp when deletion was requested
    /// </summary>
    public DateTime DeletionRequestedAt { get; set; }
    
    /// <summary>
    /// Administrator who requested deletion
    /// </summary>
    public string DeletionRequestedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// Reason provided for deletion request
    /// </summary>
    public string DeletionReason { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when deletion was approved (service marked as Deleted)
    /// </summary>
    public DateTime? DeletionApprovedAt { get; set; }
    
    /// <summary>
    /// Administrator who approved the deletion
    /// </summary>
    public string? DeletionApprovedBy { get; set; }
    
    /// <summary>
    /// Timestamp when restoration was requested
    /// </summary>
    public DateTime? RestorationRequestedAt { get; set; }
    
    /// <summary>
    /// Administrator who requested restoration
    /// </summary>
    public string? RestorationRequestedBy { get; set; }
    
    /// <summary>
    /// Reason provided for restoration request
    /// </summary>
    public string? RestorationReason { get; set; }
    
    /// <summary>
    /// Timestamp when restoration was approved (service reactivated)
    /// </summary>
    public DateTime? RestorationApprovedAt { get; set; }
    
    /// <summary>
    /// Administrator who approved the restoration
    /// </summary>
    public string? RestorationApprovedBy { get; set; }
    
    /// <summary>
    /// Restoration method used: "Quick" or "Full"
    /// </summary>
    public string? RestorationMethod { get; set; }
    
    /// <summary>
    /// Navigation property to parent service
    /// </summary>
    public Service? Service { get; set; }
}
