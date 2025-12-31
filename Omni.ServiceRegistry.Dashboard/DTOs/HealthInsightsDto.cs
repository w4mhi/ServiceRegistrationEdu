using System;
using System.Collections.Generic;

namespace Omni.ServiceRegistry.Dashboard.DTOs;

/// <summary>
/// Request to trigger health analysis
/// </summary>
public class AnalysisRequestDto
{
    public List<Guid>? ServiceIds { get; set; }
    public bool GlobalAnalysis { get; set; } = false;
}

/// <summary>
/// Response from analysis trigger
/// </summary>
public class AnalysisResponseDto
{
    public Guid RequestId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int QueueDepth { get; set; }
    public DateTime QueuedAt { get; set; }
}

/// <summary>
/// Health insight summary
/// </summary>
public class HealthInsightDto
{
    public Guid InsightId { get; set; }
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> RootCauses { get; set; } = new List<string>();
    public List<CorrelatedServiceDto> CorrelatedServices { get; set; } = new List<CorrelatedServiceDto>();
    public string HistoricalContext { get; set; } = string.Empty;
    public List<string> RecommendedActions { get; set; } = new List<string>();
    public string TriggerType { get; set; } = string.Empty;
    public string TriggeredBy { get; set; } = string.Empty;
    public string AnalysisStatus { get; set; } = string.Empty;
    public string LlmModel { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public long ProcessingTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Correlated service information
/// </summary>
public class CorrelatedServiceDto
{
    public string Service { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
}
