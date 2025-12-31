using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Api.DTOs;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;
using Omni.ServiceRegistry.Services.HealthInsights;

namespace Omni.ServiceRegistry.Api.Controllers;

[ApiController]
[Route("api/v1/insights")]
// [EnableRateLimiting("insights")]  // Temporarily disabled for testing
public class HealthInsightsController : ControllerBase
{
    private readonly IHealthInsightsAnalysisService analysisService;
    private readonly AnalysisQueueService queueService;
    private readonly IHealthInsightsRepository insightsRepository;
    private readonly IServiceRepository serviceRepository;
    private readonly IAnalysisTriggerLogRepository triggerLogRepository;
    private readonly ILogger<HealthInsightsController> logger;

    public HealthInsightsController(
        IHealthInsightsAnalysisService analysisService,
        AnalysisQueueService queueService,
        IHealthInsightsRepository insightsRepository,
        IServiceRepository serviceRepository,
        IAnalysisTriggerLogRepository triggerLogRepository,
        ILogger<HealthInsightsController> logger)
    {
        this.analysisService = analysisService;
        this.queueService = queueService;
        this.insightsRepository = insightsRepository;
        this.serviceRepository = serviceRepository;
        this.triggerLogRepository = triggerLogRepository;
        this.logger = logger;
    }

    /// <summary>
    /// Trigger health analysis for one or more services
    /// </summary>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(AnalysisResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerAnalysis([FromBody] AnalysisRequestDto request, CancellationToken cancellationToken)
    {
        string triggeredBy = User.Identity?.Name ?? "Anonymous";

        AnalysisTriggerLog? lastTrigger = await triggerLogRepository.GetLastTriggerAsync(triggeredBy);
        if (lastTrigger != null && (DateTime.UtcNow - lastTrigger.TriggeredAt).TotalSeconds < 60)
        {
            double remainingSeconds = 60 - (DateTime.UtcNow - lastTrigger.TriggeredAt).TotalSeconds;
            return Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Rate limit exceeded",
                detail: $"Please wait {remainingSeconds:F0} seconds before triggering another analysis");
        }

        List<Guid> serviceIds = new List<Guid>();

        if (request.GlobalAnalysis)
        {
            List<Service> allServices = await serviceRepository.GetAllAsync();
            serviceIds = allServices.Select(s => s.ServiceId).ToList();
        }
        else if (request.ServiceIds != null && request.ServiceIds.Count > 0)
        {
            serviceIds = request.ServiceIds;
        }
        else
        {
            return BadRequest(new { error = "Must specify either ServiceIds or GlobalAnalysis=true" });
        }

        if (serviceIds.Count == 0)
        {
            return BadRequest(new { error = "No services found to analyze" });
        }

        await triggerLogRepository.RecordTriggerAsync(new AnalysisTriggerLog
        {
            LogId = Guid.NewGuid(),
            TriggeredBy = triggeredBy,
            TriggeredAt = DateTime.UtcNow,
            RequestType = request.GlobalAnalysis ? "Global" : "PerService",
            ServiceIds = JsonSerializer.Serialize(serviceIds)
        });

        Guid requestId = Guid.NewGuid();
        foreach (Guid serviceId in serviceIds)
        {
            AnalysisRequest analysisRequest = new AnalysisRequest
            {
                RequestId = requestId,
                ServiceId = serviceId,
                TriggeredBy = triggeredBy,
                IsManualTrigger = true,
                Priority = 1
            };

            await queueService.EnqueueAsync(analysisRequest, cancellationToken);
        }

        logger.LogInformation("Queued {Count} services for analysis, triggered by {User}", 
            serviceIds.Count, triggeredBy);

        return Accepted(new AnalysisResponseDto
        {
            RequestId = requestId,
            Status = "Queued",
            Message = $"{serviceIds.Count} service(s) queued for analysis",
            QueueDepth = queueService.GetQueueDepth(),
            QueuedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get latest insight for a service
    /// </summary>
    [HttpGet("service/{serviceId}/latest")]
    [ProducesResponseType(typeof(HealthInsightDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestInsight(Guid serviceId)
    {
        ServiceHealthInsight? insight = await insightsRepository.GetLatestInsightAsync(serviceId);
        if (insight == null)
        {
            return NotFound(new { error = "No insights found for this service" });
        }

        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        
        return Ok(MapToDto(insight, service?.ServiceName ?? "Unknown"));
    }

    /// <summary>
    /// Get all insights for a service
    /// </summary>
    [HttpGet("service/{serviceId}")]
    [ProducesResponseType(typeof(List<HealthInsightDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiceInsights(Guid serviceId, [FromQuery] DateTime? since = null)
    {
        List<ServiceHealthInsight> insights = await insightsRepository.GetInsightsByServiceAsync(serviceId, since);
        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        string serviceName = service?.ServiceName ?? "Unknown";

        List<HealthInsightDto> dtos = insights.Select(i => MapToDto(i, serviceName)).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Get recent insights across all services
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(List<HealthInsightDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentInsights([FromQuery] int count = 20, [FromQuery] DateTime? since = null)
    {
        if (count > 100)
        {
            count = 100;
        }

        List<ServiceHealthInsight> insights = await insightsRepository.GetRecentInsightsAsync(count, since);
        List<HealthInsightDto> dtos = new List<HealthInsightDto>();

        foreach (ServiceHealthInsight insight in insights)
        {
            Service? service = await serviceRepository.GetByIdAsync(insight.ServiceId);
            dtos.Add(MapToDto(insight, service?.ServiceName ?? "Unknown"));
        }

        return Ok(dtos);
    }

    /// <summary>
    /// Get specific insight by ID
    /// </summary>
    [HttpGet("{insightId}")]
    [ProducesResponseType(typeof(HealthInsightDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInsight(Guid insightId)
    {
        ServiceHealthInsight? insight = await insightsRepository.GetByIdAsync(insightId);
        if (insight == null)
        {
            return NotFound(new { error = "Insight not found" });
        }

        Service? service = await serviceRepository.GetByIdAsync(insight.ServiceId);
        return Ok(MapToDto(insight, service?.ServiceName ?? "Unknown"));
    }

    private HealthInsightDto MapToDto(ServiceHealthInsight insight, string serviceName)
    {
        List<string> rootCauses = new List<string>();
        List<CorrelatedServiceDto> correlatedServices = new List<CorrelatedServiceDto>();
        List<string> recommendedActions = new List<string>();

        if (!string.IsNullOrEmpty(insight.RootCauses))
        {
            try
            {
                rootCauses = JsonSerializer.Deserialize<List<string>>(insight.RootCauses) ?? new List<string>();
            }
            catch
            {
                rootCauses = new List<string> { insight.RootCauses };
            }
        }

        if (!string.IsNullOrEmpty(insight.CorrelatedServices))
        {
            try
            {
                List<Omni.ServiceRegistry.Services.LLM.CorrelatedServiceResult>? correlated = JsonSerializer.Deserialize<List<Omni.ServiceRegistry.Services.LLM.CorrelatedServiceResult>>(insight.CorrelatedServices);
                if (correlated != null)
                {
                    correlatedServices = correlated.Select(c => new CorrelatedServiceDto
                    {
                        Service = c.Service,
                        Impact = c.Impact
                    }).ToList();
                }
            }
            catch
            {
                // Ignore parse errors
            }
        }

        if (!string.IsNullOrEmpty(insight.RecommendedActions))
        {
            try
            {
                recommendedActions = JsonSerializer.Deserialize<List<string>>(insight.RecommendedActions) ?? new List<string>();
            }
            catch
            {
                recommendedActions = new List<string> { insight.RecommendedActions };
            }
        }

        return new HealthInsightDto
        {
            InsightId = insight.InsightId,
            ServiceId = insight.ServiceId,
            ServiceName = serviceName,
            GeneratedAt = insight.GeneratedAt,
            Summary = insight.Summary ?? string.Empty,
            RootCauses = rootCauses,
            CorrelatedServices = correlatedServices,
            HistoricalContext = insight.HistoricalContext ?? string.Empty,
            RecommendedActions = recommendedActions,
            TriggerType = insight.TriggerType ?? string.Empty,
            TriggeredBy = insight.TriggeredBy ?? string.Empty,
            AnalysisStatus = insight.AnalysisStatus ?? string.Empty,
            LlmModel = insight.LlmModel ?? string.Empty,
            TokensUsed = insight.TokensUsed,
            ProcessingTimeMs = insight.ProcessingTimeMs,
            ErrorMessage = insight.ErrorMessage
        };
    }
}
