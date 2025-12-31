using System;

namespace Omni.ServiceRegistry.Dashboard.DTOs;

/// <summary>
/// DTO for service deletion cycle information
/// </summary>
public class ServiceDeletionCycleDto
{
    public Guid ServiceDeletionCycleId { get; set; }
    public Guid ServiceId { get; set; }
    public int CycleNumber { get; set; }
    
    // Deletion information
    public DateTime DeletionRequestedAt { get; set; }
    public string DeletionRequestedBy { get; set; } = string.Empty;
    public string DeletionReason { get; set; } = string.Empty;
    public DateTime? DeletionApprovedAt { get; set; }
    public string? DeletionApprovedBy { get; set; }
    
    // Restoration information
    public DateTime? RestorationRequestedAt { get; set; }
    public string? RestorationRequestedBy { get; set; }
    public string? RestorationReason { get; set; }
    public DateTime? RestorationApprovedAt { get; set; }
    public string? RestorationApprovedBy { get; set; }
    public string? RestorationMethod { get; set; } // "Quick" or "Full"
}
