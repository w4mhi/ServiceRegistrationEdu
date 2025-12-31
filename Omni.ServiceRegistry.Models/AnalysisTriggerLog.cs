using System;

namespace Omni.ServiceRegistry.Models;

/// <summary>
/// Tracks admin-triggered analysis requests for cooldown enforcement
/// </summary>
public class AnalysisTriggerLog
{
    /// <summary>
    /// Unique identifier for this trigger log entry
    /// </summary>
    public Guid LogId { get; set; }

    /// <summary>
    /// Admin who triggered the analysis
    /// </summary>
    public string TriggeredBy { get; set; } = string.Empty;

    /// <summary>
    /// When the analysis was triggered
    /// </summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>
    /// Type of request: "Global" or "PerService"
    /// </summary>
    public string RequestType { get; set; } = string.Empty;

    /// <summary>
    /// Service IDs included in this request (JSON array, null for global)
    /// </summary>
    public string? ServiceIds { get; set; }
}
