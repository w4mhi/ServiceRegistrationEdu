using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;
using Omni.ServiceRegistry.Services.LLM;

namespace Omni.ServiceRegistry.Services.HealthInsights;

/// <summary>
/// Orchestrates health analysis using LLM with noise reduction
/// </summary>
public class HealthInsightsAnalysisService : IHealthInsightsAnalysisService
{
    private readonly OllamaService ollamaService;
    private readonly ContextGathererService contextGathererService;
    private readonly PromptTemplateService promptTemplateService;
    private readonly IHealthInsightsRepository insightsRepository;
    private readonly IServiceRepository serviceRepository;
    private readonly IChangeHistoryRepository changeHistoryRepository;
    private readonly IConfiguration configuration;
    private readonly ILogger<HealthInsightsAnalysisService>? logger;

    public HealthInsightsAnalysisService(
        IConfiguration configuration,
        OllamaService ollamaService,
        ContextGathererService contextGathererService,
        PromptTemplateService promptTemplateService,
        IHealthInsightsRepository insightsRepository,
        IServiceRepository serviceRepository,
        IChangeHistoryRepository changeHistoryRepository,
        ILogger<HealthInsightsAnalysisService>? logger = null)
    {
        this.configuration = configuration;
        this.ollamaService = ollamaService;
        this.contextGathererService = contextGathererService;
        this.promptTemplateService = promptTemplateService;
        this.insightsRepository = insightsRepository;
        this.serviceRepository = serviceRepository;
        this.changeHistoryRepository = changeHistoryRepository;
        this.logger = logger;
    }

    public async Task<ServiceHealthInsight> AnalyzeServiceAsync(
        Guid serviceId, 
        string triggeredBy, 
        bool isManualTrigger, 
        CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("Starting analysis for service {ServiceId}, trigger={TriggerType}, by={TriggeredBy}", 
            serviceId, isManualTrigger ? "Manual" : "Automatic", triggeredBy);

        bool shouldSkip = await ShouldSkipAnalysisAsync(serviceId, "PreAnalysisCheck");
        if (shouldSkip)
        {
            logger?.LogInformation("Skipping analysis for service {ServiceId} due to noise reduction", serviceId);
            throw new InvalidOperationException("Analysis skipped: noise reduction filter triggered");
        }

        int cacheHours = configuration.GetValue<int>("HealthInsights:CacheResultsHours", 24);
        DateTime cacheThreshold = DateTime.UtcNow.AddHours(-cacheHours);

        ServiceHealthInsight? cachedInsight = await insightsRepository.GetLatestInsightAsync(serviceId);
        if (cachedInsight != null && cachedInsight.GeneratedAt >= cacheThreshold && cachedInsight.AnalysisStatus == "Completed")
        {
            logger?.LogInformation("Using cached insight for service {ServiceId}, age={AgeMinutes}min", 
                serviceId, (DateTime.UtcNow - cachedInsight.GeneratedAt).TotalMinutes);
            return cachedInsight;
        }

        Guid insightId = Guid.NewGuid();
        ServiceHealthInsight insight = new ServiceHealthInsight
        {
            InsightId = insightId,
            ServiceId = serviceId,
            GeneratedAt = DateTime.UtcNow,
            TriggerType = isManualTrigger ? "Manual" : "Automatic",
            TriggeredBy = triggeredBy,
            AnalysisStatus = "Processing",
            RootCauses = "[]",
            CorrelatedServices = "[]",
            RecommendedActions = "[]",
            ContextData = "{}",
            HistoricalContext = "No historical data yet"
        };

        logger?.LogInformation("BEFORE AddInsightAsync - InsightId: {InsightId}, ServiceId: {ServiceId}", insightId, serviceId);
        await insightsRepository.AddInsightAsync(insight);
        logger?.LogInformation("AFTER AddInsightAsync - InsightId: {InsightId}", insightId);

        try
        {
            AnalysisContext context = await contextGathererService.GatherContextAsync(serviceId, isManualTrigger);

            string prompt = promptTemplateService.BuildCompletePrompt(context);
            int estimatedTokens = promptTemplateService.EstimateTokenCount(prompt);

            logger?.LogInformation("Generated prompt: {Tokens} tokens for service {ServiceId}", estimatedTokens, serviceId);

            OllamaResponse response = await ollamaService.GenerateAsync(prompt, cancellationToken);

            AnalysisResult? parsedResult = promptTemplateService.ParseResponse(response.Response);

            if (parsedResult == null)
            {
                throw new InvalidOperationException("Failed to parse LLM response as JSON");
            }

            insight.Summary = parsedResult.Summary;
            insight.RootCauses = JsonSerializer.Serialize(parsedResult.RootCauses);
            insight.CorrelatedServices = JsonSerializer.Serialize(parsedResult.CorrelatedServices);
            insight.HistoricalContext = parsedResult.HistoricalContext;
            insight.RecommendedActions = JsonSerializer.Serialize(parsedResult.RecommendedActions);
            insight.LlmModel = response.Model;
            insight.TokensUsed = response.TokensUsed;
            insight.ProcessingTimeMs = response.ProcessingTimeMs;
            insight.AnalysisStatus = "Completed";
            insight.ContextData = JsonSerializer.Serialize(context);

            logger?.LogInformation("About to save insight - RootCauses length: {RC}, CorrelatedServices length: {CS}, RecommendedActions length: {RA}",
                insight.RootCauses?.Length ?? 0, insight.CorrelatedServices?.Length ?? 0, insight.RecommendedActions?.Length ?? 0);
            logger?.LogInformation("CorrelatedServices JSON: {Json}", insight.CorrelatedServices);

            logger?.LogInformation("BEFORE UpdateInsightAsync - InsightId: {InsightId}, Status: {Status}", insight.InsightId, insight.AnalysisStatus);
            await insightsRepository.UpdateInsightAsync(insight);
            logger?.LogInformation("AFTER UpdateInsightAsync - InsightId: {InsightId}", insight.InsightId);

            logger?.LogInformation("Analysis completed for service {ServiceId}: {Tokens} tokens, {TimeMs}ms", 
                serviceId, response.TokensUsed, response.ProcessingTimeMs);

            return insight;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Analysis failed for service {ServiceId}", serviceId);

            insight.AnalysisStatus = "Failed";
            insight.ErrorMessage = ex.Message.Length > 1000 ? ex.Message.Substring(0, 1000) : ex.Message;
            await insightsRepository.UpdateInsightAsync(insight);

            throw;
        }
    }

    public async Task<List<ServiceHealthInsight>> AnalyzeMultipleServicesAsync(
        List<Guid> serviceIds, 
        string triggeredBy, 
        CancellationToken cancellationToken = default)
    {
        List<ServiceHealthInsight> insights = new List<ServiceHealthInsight>();

        foreach (Guid serviceId in serviceIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                ServiceHealthInsight insight = await AnalyzeServiceAsync(serviceId, triggeredBy, true, cancellationToken);
                insights.Add(insight);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to analyze service {ServiceId} in batch", serviceId);
            }
        }

        return insights;
    }

    /// <summary>
    /// Noise reduction: check if analysis should be skipped
    /// </summary>
    public async Task<bool> ShouldSkipAnalysisAsync(Guid serviceId, string reason)
    {
        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        if (service == null)
        {
            logger?.LogWarning("Service {ServiceId} not found, skipping analysis", serviceId);
            return true;
        }

        int minHeartbeats = configuration.GetValue<int>("HealthInsights:MinHeartbeatsForAnalysis", 10);
        if (service.HeartbeatCount < minHeartbeats)
        {
            logger?.LogInformation("Service {ServiceId} has only {Count} heartbeats (min={Min}), skipping analysis", 
                serviceId, service.HeartbeatCount, minHeartbeats);
            return true;
        }

        List<ServiceChangeHistory> recentChanges = await changeHistoryRepository.GetByServiceIdAsync(serviceId);
        List<ServiceChangeHistory> recentHealthChanges = recentChanges
            .Where(c => c.FieldName == "HealthStatus" && c.ChangedAt >= DateTime.UtcNow.AddHours(-1))
            .OrderByDescending(c => c.ChangedAt)
            .ToList();

        if (recentHealthChanges.Count == 2)
        {
            string firstStatus = recentHealthChanges[1].NewValue ?? string.Empty;
            string secondStatus = recentHealthChanges[0].NewValue ?? string.Empty;

            if (firstStatus == "UNHEALTHY" && secondStatus == "HEALTHY")
            {
                logger?.LogInformation("Service {ServiceId} had transient UNHEALTHY (recovered in 1 hour), skipping", 
                    serviceId);
                return true;
            }
        }

        List<ServiceHealthInsight> recentInsights = await insightsRepository.GetInsightsByServiceAsync(
            serviceId, 
            DateTime.UtcNow.AddHours(-1));

        if (recentInsights.Count > 0)
        {
            ServiceHealthInsight latestInsight = recentInsights[0];
            string latestPattern = ExtractFailurePattern(latestInsight);
            string currentPattern = ExtractCurrentFailurePattern(recentHealthChanges);

            if (!string.IsNullOrEmpty(latestPattern) && latestPattern == currentPattern)
            {
                logger?.LogInformation("Service {ServiceId} has duplicate failure pattern within 1 hour, skipping", 
                    serviceId);
                return true;
            }
        }

        return false;
    }

    private string ExtractFailurePattern(ServiceHealthInsight insight)
    {
        if (string.IsNullOrEmpty(insight.Summary))
        {
            return string.Empty;
        }

        string summary = insight.Summary.ToLowerInvariant();
        
        if (summary.Contains("timeout") || summary.Contains("heartbeat"))
        {
            return "TIMEOUT";
        }
        if (summary.Contains("degraded"))
        {
            return "DEGRADED";
        }
        if (summary.Contains("intermittent") || summary.Contains("flapping"))
        {
            return "FLAPPING";
        }
        
        return "GENERAL";
    }

    private string ExtractCurrentFailurePattern(List<ServiceChangeHistory> changes)
    {
        if (changes.Count < 2)
        {
            return string.Empty;
        }

        List<string> statuses = changes
            .OrderBy(c => c.ChangedAt)
            .Select(c => c.NewValue ?? string.Empty)
            .ToList();

        if (statuses.Contains("DEGRADED"))
        {
            return "DEGRADED";
        }

        int healthyUnhealthyCount = statuses.Count(s => s == "HEALTHY" || s == "UNHEALTHY");
        if (healthyUnhealthyCount >= 3)
        {
            return "FLAPPING";
        }

        if (statuses.Contains("DEAD"))
        {
            return "TIMEOUT";
        }

        return "GENERAL";
    }

    public async Task<bool> CheckOllamaHealthAsync()
    {
        try
        {
            return await ollamaService.IsAvailableAsync();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ollama health check failed");
            return false;
        }
    }
}
