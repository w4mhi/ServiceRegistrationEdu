using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Models;

/// <summary>
/// Audit log of service modifications (R30, R61)
/// </summary>
public class ServiceChangeHistory
{
    /// <summary>
    /// Unique change record identifier
    /// </summary>
    public Guid ChangeId { get; set; }
    
    /// <summary>
    /// Service that was modified
    /// </summary>
    public Guid ServiceId { get; set; }
    
    /// <summary>
    /// Type of change (e.g., "StatusChange", "ConfigUpdate", "EndpointUpdate")
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of the change
    /// </summary>
    public string ChangeDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Field that was modified
    /// </summary>
    public string? FieldName { get; set; }
    
    /// <summary>
    /// Previous value (JSON serialized)
    /// </summary>
    public string? OldValue { get; set; }
    
    /// <summary>
    /// New value (JSON serialized)
    /// </summary>
    public string? NewValue { get; set; }
    
    /// <summary>
    /// User or system that made the change
    /// </summary>
    public string ChangedBy { get; set; } = "System";
    
    /// <summary>
    /// Timestamp when change occurred
    /// </summary>
    public DateTime ChangedAt { get; set; }
}
