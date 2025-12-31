using System;
using System.Collections.Generic;

namespace Omni.ServiceRegistry.Services.HealthInsights;

/// <summary>
/// Structured context data for health analysis
/// </summary>
public class AnalysisContext
{
    /// <summary>
    /// Layer 1: Service identity and configuration (~500 tokens)
    /// </summary>
    public ServiceIdentity ServiceIdentity { get; set; } = new ServiceIdentity();

    /// <summary>
    /// Layer 2: Recent health timeline (~2000 tokens)
    /// </summary>
    public List<HealthTimelineEvent> HealthTimeline { get; set; } = new List<HealthTimelineEvent>();

    /// <summary>
    /// Layer 3: Historical patterns (~500 tokens)
    /// </summary>
    public HistoricalPatterns HistoricalPatterns { get; set; } = new HistoricalPatterns();

    /// <summary>
    /// Layer 4: Correlation context (~1000 tokens)
    /// </summary>
    public List<CorrelatedService> CorrelatedServices { get; set; } = new List<CorrelatedService>();

    /// <summary>
    /// Layer 5: Recent changes (~500 tokens)
    /// </summary>
    public List<ServiceChange> RecentChanges { get; set; } = new List<ServiceChange>();

    /// <summary>
    /// Total estimated token count for this context
    /// </summary>
    public int EstimatedTokenCount { get; set; }
}

public class ServiceIdentity
{
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Endpoints { get; set; } = new List<string>();
    public int HeartbeatTimeout { get; set; }
    public int MaxMissedHeartbeats { get; set; }
    public string CurrentHealthStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long TotalHeartbeats { get; set; }
}

public class HealthTimelineEvent
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty; // "StatusChange", "HeartbeatMissed", "Recovered"
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public int MissedHeartbeatCount { get; set; }
    public string? Notes { get; set; }
}

public class HistoricalPatterns
{
    public int TotalStatusChanges { get; set; }
    public int TotalUnhealthyEpisodes { get; set; }
    public double AverageRecoveryTimeMinutes { get; set; }
    public string MostFrequentFailurePattern { get; set; } = string.Empty;
    public DateTime? LastDegradedDate { get; set; }
    public int DaysSinceLastIncident { get; set; }
}

public class CorrelatedService
{
    public string ServiceName { get; set; } = string.Empty;
    public string HealthStatus { get; set; } = string.Empty;
    public DateTime? StatusChangedAt { get; set; }
    public string CorrelationType { get; set; } = string.Empty; // "SimultaneousFailure", "SequentialFailure", "SharedDependency"
    public int TimeDifferenceSeconds { get; set; }
}

public class ServiceChange
{
    public DateTime ChangedAt { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string ChangeDescription { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
}
