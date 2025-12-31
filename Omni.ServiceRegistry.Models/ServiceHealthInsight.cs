using System;

namespace Omni.ServiceRegistry.Models;

/// <summary>
/// Represents an AI-generated health analysis insight for a service
/// </summary>
public class ServiceHealthInsight
{
    /// <summary>
    /// Unique identifier for this insight
    /// </summary>
    public Guid InsightId { get; set; }

    /// <summary>
    /// The service this insight is about
    /// </summary>
    public Guid ServiceId { get; set; }

    /// <summary>
    /// When this insight was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// How this analysis was triggered: "RealTime", "Batch", "Manual"
    /// </summary>
    public string TriggerType { get; set; } = string.Empty;

    /// <summary>
    /// Admin who triggered manual analysis (null for automatic)
    /// </summary>
    public string? TriggeredBy { get; set; }

    /// <summary>
    /// Human-readable incident summary (2-3 sentences)
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Potential root causes (JSON array of ranked causes)
    /// </summary>
    public string? RootCauses { get; set; }

    /// <summary>
    /// Related service impact analysis (JSON array of service names and impact descriptions)
    /// </summary>
    public string? CorrelatedServices { get; set; }

    /// <summary>
    /// Historical pattern insights (plain text or JSON)
    /// </summary>
    public string? HistoricalContext { get; set; }

    /// <summary>
    /// Recommended actions for administrators (JSON array of actionable steps)
    /// </summary>
    public string? RecommendedActions { get; set; }

    /// <summary>
    /// LLM model used for analysis (e.g., "phi4", "llama3")
    /// </summary>
    public string LlmModel { get; set; } = string.Empty;

    /// <summary>
    /// Number of tokens consumed by this analysis
    /// </summary>
    public int TokensUsed { get; set; }

    /// <summary>
    /// Time taken to process this analysis (milliseconds)
    /// </summary>
    public int ProcessingTimeMs { get; set; }

    /// <summary>
    /// Analysis status: "Queued", "Processing", "Completed", "Failed"
    /// </summary>
    public string AnalysisStatus { get; set; } = "Queued";

    /// <summary>
    /// Error message if analysis failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Full context data used for analysis (JSON snapshot for audit)
    /// </summary>
    public string? ContextData { get; set; }

    /// <summary>
    /// Navigation property to the associated service
    /// </summary>
    public Service? Service { get; set; }
}
