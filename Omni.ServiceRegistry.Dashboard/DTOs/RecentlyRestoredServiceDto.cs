using System;

namespace Omni.ServiceRegistry.Dashboard.DTOs;

/// <summary>
/// DTO for recently restored service badge information
/// </summary>
public class RecentlyRestoredServiceDto
{
    /// <summary>
    /// Service identifier
    /// </summary>
    public Guid ServiceId { get; set; }
    
    /// <summary>
    /// Restoration method used: "Quick" or "Full"
    /// </summary>
    public string RestorationMethod { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when restoration was approved
    /// </summary>
    public DateTime RestorationApprovedAt { get; set; }
    
    /// <summary>
    /// Days since restoration
    /// </summary>
    public int DaysAgo { get; set; }
    
    /// <summary>
    /// Days until badge expires
    /// </summary>
    public int BadgeExpiresIn { get; set; }
}
