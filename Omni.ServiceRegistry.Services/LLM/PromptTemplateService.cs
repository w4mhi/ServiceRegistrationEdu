using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Omni.ServiceRegistry.Services.LLM;

/// <summary>
/// Creates prompts for LLM health analysis
/// </summary>
public class PromptTemplateService
{
    private readonly ILogger<PromptTemplateService>? logger;
    private readonly string configuredSystemPrompt;

    public PromptTemplateService(string? systemPrompt = null, ILogger<PromptTemplateService>? logger = null)
    {
        this.logger = logger;
        this.configuredSystemPrompt = systemPrompt ?? GetDefaultSystemPrompt();
    }

    /// <summary>
    /// System prompt defining the LLM's role and output format
    /// </summary>
    public string GetSystemPrompt()
    {
        return configuredSystemPrompt;
    }

    /// <summary>
    /// Default system prompt if not configured
    /// </summary>
    private string GetDefaultSystemPrompt()
    {
        return @"You are a service health analyst for a microservices platform. 
Analyze service health and provide:
1. Brief incident summary (1-2 sentences)
2. Top 3 root causes (ranked by likelihood)
3. Related service impact
4. Historical insights (1 sentence)
5. Top 3 recommended actions

Output MUST be valid JSON:
{
  ""summary"": ""..."",
  ""rootCauses"": [""...""],
  ""correlatedServices"": [{""service"": ""..."", ""impact"": ""...""}],
  ""historicalContext"": ""..."",
  ""recommendedActions"": [""...""]
}

Be concise, technical, and actionable. Focus on facts from the data.";
    }

    /// <summary>
    /// Build user prompt with context data
    /// </summary>
    public string BuildUserPrompt(HealthInsights.AnalysisContext context)
    {
        StringBuilder prompt = new StringBuilder();

        prompt.AppendLine("# Service Health Analysis Request");
        prompt.AppendLine();

        // Layer 1: Service Identity
        prompt.AppendLine("## Service Information");
        prompt.AppendLine($"- **Name**: {context.ServiceIdentity.ServiceName}");
        prompt.AppendLine($"- **ID**: {context.ServiceIdentity.ServiceId}");
        prompt.AppendLine($"- **Description**: {context.ServiceIdentity.Description}");
        prompt.AppendLine($"- **Current Status**: {context.ServiceIdentity.CurrentHealthStatus}");
        prompt.AppendLine($"- **Heartbeat Config**: Timeout={context.ServiceIdentity.HeartbeatTimeout}s, Max Missed={context.ServiceIdentity.MaxMissedHeartbeats}");
        prompt.AppendLine($"- **Total Heartbeats**: {context.ServiceIdentity.TotalHeartbeats:N0}");
        prompt.AppendLine($"- **Endpoints**: {string.Join(", ", context.ServiceIdentity.Endpoints)}");
        prompt.AppendLine($"- **Age**: {(DateTime.UtcNow - context.ServiceIdentity.CreatedAt).TotalDays:F0} days");
        prompt.AppendLine();

        // Layer 2: Health Timeline
        if (context.HealthTimeline.Count > 0)
        {
            prompt.AppendLine("## Recent Health Events");
            foreach (HealthInsights.HealthTimelineEvent evt in context.HealthTimeline.Take(20))
            {
                prompt.AppendLine($"- **{evt.Timestamp:yyyy-MM-dd HH:mm}**: {evt.FromStatus} → {evt.ToStatus}");
                if (!string.IsNullOrWhiteSpace(evt.Notes))
                {
                    prompt.AppendLine($"  {evt.Notes}");
                }
            }
            prompt.AppendLine();
        }

        // Layer 3: Historical Patterns
        prompt.AppendLine("## Historical Patterns");
        prompt.AppendLine($"- **Total Status Changes**: {context.HistoricalPatterns.TotalStatusChanges}");
        prompt.AppendLine($"- **Unhealthy Episodes**: {context.HistoricalPatterns.TotalUnhealthyEpisodes}");
        prompt.AppendLine($"- **Average Recovery Time**: {context.HistoricalPatterns.AverageRecoveryTimeMinutes:F1} minutes");
        prompt.AppendLine($"- **Failure Pattern**: {context.HistoricalPatterns.MostFrequentFailurePattern}");
        if (context.HistoricalPatterns.LastDegradedDate.HasValue)
        {
            prompt.AppendLine($"- **Last Incident**: {context.HistoricalPatterns.DaysSinceLastIncident} days ago ({context.HistoricalPatterns.LastDegradedDate:yyyy-MM-dd})");
        }
        prompt.AppendLine();

        // Layer 4: Correlated Services
        if (context.CorrelatedServices.Count > 0)
        {
            prompt.AppendLine("## Correlated Service Failures");
            foreach (HealthInsights.CorrelatedService corr in context.CorrelatedServices)
            {
                prompt.AppendLine($"- **{corr.ServiceName}**: {corr.CorrelationType} (Δ{corr.TimeDifferenceSeconds}s, now {corr.HealthStatus})");
            }
            prompt.AppendLine();
        }

        // Layer 5: Recent Changes
        if (context.RecentChanges.Count > 0)
        {
            prompt.AppendLine("## Recent Service Changes");
            foreach (HealthInsights.ServiceChange change in context.RecentChanges)
            {
                prompt.AppendLine($"- **{change.ChangedAt:yyyy-MM-dd HH:mm}** [{change.ChangeType}]: {change.ChangeDescription} (by {change.ChangedBy})");
            }
            prompt.AppendLine();
        }

        prompt.AppendLine("---");
        prompt.AppendLine("Based on the above data, provide your analysis in JSON format as specified in the system prompt.");

        string result = prompt.ToString();
        logger?.LogDebug("Generated prompt: {Length} characters", result.Length);

        return result;
    }

    /// <summary>
    /// Build complete prompt (system + user)
    /// </summary>
    public string BuildCompletePrompt(HealthInsights.AnalysisContext context)
    {
        StringBuilder prompt = new StringBuilder();
        prompt.AppendLine(GetSystemPrompt());
        prompt.AppendLine();
        prompt.AppendLine("---");
        prompt.AppendLine();
        prompt.AppendLine(BuildUserPrompt(context));

        return prompt.ToString();
    }

    /// <summary>
    /// Estimate token count (rough: 1 token ≈ 4 characters)
    /// </summary>
    public int EstimateTokenCount(string prompt)
    {
        return prompt.Length / 4;
    }

    /// <summary>
    /// Parse LLM JSON response
    /// </summary>
    public AnalysisResult? ParseResponse(string response)
    {
        try
        {
            // Try to extract JSON from response (LLM might add extra text)
            int jsonStart = response.IndexOf('{');
            int jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                string jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                
                AnalysisResult? result = JsonSerializer.Deserialize<AnalysisResult>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result;
            }

            logger?.LogWarning("No JSON found in LLM response");
            return null;
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "Failed to parse LLM response as JSON");
            return null;
        }
    }
}

/// <summary>
/// Parsed analysis result from LLM
/// </summary>
public class AnalysisResult
{
    public string Summary { get; set; } = string.Empty;
    public List<string> RootCauses { get; set; } = new List<string>();
    public List<CorrelatedServiceResult> CorrelatedServices { get; set; } = new List<CorrelatedServiceResult>();
    public string HistoricalContext { get; set; } = string.Empty;
    public List<string> RecommendedActions { get; set; } = new List<string>();
}

public class CorrelatedServiceResult
{
    public string Service { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
}
