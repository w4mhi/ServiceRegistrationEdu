using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Dashboard.DTOs;

namespace Omni.ServiceRegistry.Dashboard.Services;

/// <summary>
/// API client for health insights endpoints
/// </summary>
public class HealthInsightsApiClient
{
    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly ILogger<HealthInsightsApiClient> logger;

    public HealthInsightsApiClient(IConfiguration configuration, HttpClient httpClient, ILogger<HealthInsightsApiClient> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
        // BaseAddress is already set by Program.cs, use empty string for relative URLs
        baseUrl = "";
    }

    /// <summary>
    /// Trigger analysis for specific services
    /// </summary>
    public async Task<AnalysisResponseDto?> TriggerAnalysisAsync(List<Guid> serviceIds)
    {
        try
        {
            AnalysisRequestDto request = new AnalysisRequestDto
            {
                ServiceIds = serviceIds,
                GlobalAnalysis = false
            };

            HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/v1/insights/analyze", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AnalysisResponseDto>();
            }

            string errorContent = await response.Content.ReadAsStringAsync();
            logger.LogWarning("Analysis trigger failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger analysis for services");
            return null;
        }
    }

    /// <summary>
    /// Trigger global analysis for all services
    /// </summary>
    public async Task<AnalysisResponseDto?> TriggerGlobalAnalysisAsync()
    {
        try
        {
            AnalysisRequestDto request = new AnalysisRequestDto
            {
                GlobalAnalysis = true
            };

            HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/v1/insights/analyze", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AnalysisResponseDto>();
            }

            string errorContent = await response.Content.ReadAsStringAsync();
            logger.LogWarning("Global analysis trigger failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger global analysis");
            return null;
        }
    }

    /// <summary>
    /// Get latest insight for a service
    /// </summary>
    public async Task<HealthInsightDto?> GetLatestInsightAsync(Guid serviceId)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<HealthInsightDto>($"api/v1/insights/service/{serviceId}/latest");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get latest insight for service {ServiceId}", serviceId);
            return null;
        }
    }

    /// <summary>
    /// Get all insights for a service
    /// </summary>
    public async Task<List<HealthInsightDto>> GetServiceInsightsAsync(Guid serviceId, DateTime? since = null)
    {
        try
        {
            string url = $"api/v1/insights/service/{serviceId}";
            if (since.HasValue)
            {
                url += $"?since={since.Value:O}";
            }

            List<HealthInsightDto>? insights = await httpClient.GetFromJsonAsync<List<HealthInsightDto>>(url);
            return insights ?? new List<HealthInsightDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get insights for service {ServiceId}", serviceId);
            return new List<HealthInsightDto>();
        }
    }

    /// <summary>
    /// Get recent insights across all services
    /// </summary>
    public async Task<List<HealthInsightDto>> GetRecentInsightsAsync(int count = 20, DateTime? since = null)
    {
        try
        {
            string url = $"api/v1/insights/recent?count={count}";
            if (since.HasValue)
            {
                url += $"&since={since.Value:O}";
            }

            logger.LogInformation("Calling insights API: {Url}", url);
            List<HealthInsightDto>? insights = await httpClient.GetFromJsonAsync<List<HealthInsightDto>>(url);
            logger.LogInformation("Received {Count} insights from API", insights?.Count ?? 0);
            return insights ?? new List<HealthInsightDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get recent insights");
            return new List<HealthInsightDto>();
        }
    }

    /// <summary>
    /// Get specific insight by ID
    /// </summary>
    public async Task<HealthInsightDto?> GetInsightAsync(Guid insightId)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<HealthInsightDto>($"api/v1/insights/{insightId}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get insight {InsightId}", insightId);
            return null;
        }
    }
}
