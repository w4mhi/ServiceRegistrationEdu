using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Services.HealthInsights;

/// <summary>
/// Gathers context data for health analysis (5-layer approach)
/// </summary>
public class ContextGathererService
{
    private readonly IServiceRepository serviceRepository;
    private readonly IChangeHistoryRepository changeHistoryRepository;
    private readonly IConfiguration configuration;
    private readonly ILogger<ContextGathererService>? logger;

    public ContextGathererService(
        IServiceRepository serviceRepository,
        IChangeHistoryRepository changeHistoryRepository,
        IConfiguration configuration,
        ILogger<ContextGathererService>? logger = null)
    {
        this.serviceRepository = serviceRepository;
        this.changeHistoryRepository = changeHistoryRepository;
        this.configuration = configuration;
        this.logger = logger;
    }

    /// <summary>
    /// Gather complete context for analysis
    /// </summary>
    public async Task<AnalysisContext> GatherContextAsync(Guid serviceId, bool isManualTrigger)
    {
        logger?.LogInformation("Gathering analysis context for service {ServiceId}, manual={IsManual}", 
            serviceId, isManualTrigger);

        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        if (service == null)
        {
            throw new InvalidOperationException($"Service {serviceId} not found");
        }

        int analysisWindowHours = isManualTrigger
            ? configuration.GetValue<int>("HealthInsights:ManualTriggerMaxWindowHours", 168)
            : configuration.GetValue<int>("HealthInsights:AnalysisWindowHours", 48);

        DateTime since = DateTime.UtcNow.AddHours(-analysisWindowHours);

        AnalysisContext context = new AnalysisContext
        {
            ServiceIdentity = await GatherServiceIdentityAsync(service),
            HealthTimeline = await GatherHealthTimelineAsync(service, since),
            HistoricalPatterns = await GatherHistoricalPatternsAsync(service),
            CorrelatedServices = await GatherCorrelatedServicesAsync(service, since),
            RecentChanges = await GatherRecentChangesAsync(service, since)
        };

        context.EstimatedTokenCount = EstimateTokenCount(context);

        logger?.LogInformation("Context gathered: {TokenCount} estimated tokens, {Events} events, {Correlated} correlated services",
            context.EstimatedTokenCount, context.HealthTimeline.Count, context.CorrelatedServices.Count);

        return context;
    }

    /// <summary>
    /// Layer 1: Service identity and configuration
    /// </summary>
    private Task<ServiceIdentity> GatherServiceIdentityAsync(Service service)
    {
        List<string> endpoints = JsonSerializer.Deserialize<List<string>>(service.Endpoints) ?? new List<string>();

        ServiceIdentity identity = new ServiceIdentity
        {
            ServiceId = service.ServiceId,
            ServiceName = service.ServiceName,
            Description = service.Description,
            Endpoints = endpoints,
            HeartbeatTimeout = service.HeartbeatTimeout,
            MaxMissedHeartbeats = service.MaxMissedHeartbeats,
            CurrentHealthStatus = service.HealthStatus.ToString(),
            CreatedAt = service.CreatedAt,
            TotalHeartbeats = service.HeartbeatCount
        };

        return Task.FromResult(identity);
    }

    /// <summary>
    /// Layer 2: Recent health timeline from change history
    /// </summary>
    private async Task<List<HealthTimelineEvent>> GatherHealthTimelineAsync(Service service, DateTime since)
    {
        List<ServiceChangeHistory> changes = await changeHistoryRepository.GetByServiceIdAsync(service.ServiceId);

        List<HealthTimelineEvent> timeline = changes
            .Where(c => c.FieldName == "HealthStatus" && c.ChangedAt >= since)
            .OrderByDescending(c => c.ChangedAt)
            .Take(50) // Limit to 50 most recent events
            .Select(c => new HealthTimelineEvent
            {
                Timestamp = c.ChangedAt,
                EventType = "StatusChange",
                FromStatus = c.OldValue ?? "Unknown",
                ToStatus = c.NewValue ?? "Unknown",
                Notes = c.ChangeDescription
            })
            .ToList();

        return timeline;
    }

    /// <summary>
    /// Layer 3: Historical patterns
    /// </summary>
    private async Task<HistoricalPatterns> GatherHistoricalPatternsAsync(Service service)
    {
        List<ServiceChangeHistory> allChanges = await changeHistoryRepository.GetByServiceIdAsync(service.ServiceId);

        List<ServiceChangeHistory> healthChanges = allChanges
            .Where(c => c.FieldName == "HealthStatus")
            .OrderBy(c => c.ChangedAt)
            .ToList();

        int totalStatusChanges = healthChanges.Count;
        int unhealthyEpisodes = healthChanges.Count(c => c.NewValue == "Unhealthy" || c.NewValue == "Degraded" || c.NewValue == "Dead");

        double averageRecoveryTime = CalculateAverageRecoveryTime(healthChanges);

        ServiceChangeHistory? lastDegraded = healthChanges
            .Where(c => c.NewValue == "Degraded" || c.NewValue == "Dead")
            .OrderByDescending(c => c.ChangedAt)
            .FirstOrDefault();

        int daysSinceLastIncident = lastDegraded != null
            ? (int)(DateTime.UtcNow - lastDegraded.ChangedAt).TotalDays
            : -1;

        return new HistoricalPatterns
        {
            TotalStatusChanges = totalStatusChanges,
            TotalUnhealthyEpisodes = unhealthyEpisodes,
            AverageRecoveryTimeMinutes = averageRecoveryTime,
            MostFrequentFailurePattern = DetermineFailurePattern(healthChanges),
            LastDegradedDate = lastDegraded?.ChangedAt,
            DaysSinceLastIncident = daysSinceLastIncident
        };
    }

    /// <summary>
    /// Layer 4: Correlated services (services with similar timing of failures)
    /// </summary>
    private async Task<List<CorrelatedService>> GatherCorrelatedServicesAsync(Service service, DateTime since)
    {
        List<Service> allServices = await serviceRepository.GetAllActiveAsync();
        List<CorrelatedService> correlatedServices = new List<CorrelatedService>();

        // Find services that changed status around the same time
        List<ServiceChangeHistory> serviceHealthChanges = await changeHistoryRepository.GetByServiceIdAsync(service.ServiceId);
        List<ServiceChangeHistory> criticalChanges = serviceHealthChanges
            .Where(c => c.FieldName == "HealthStatus" 
                && c.ChangedAt >= since
                && (c.NewValue == "Unhealthy" || c.NewValue == "Degraded" || c.NewValue == "Dead"))
            .ToList();

        foreach (ServiceChangeHistory change in criticalChanges.Take(5))
        {
            foreach (Service otherService in allServices.Where(s => s.ServiceId != service.ServiceId).Take(20))
            {
                List<ServiceChangeHistory> otherChanges = await changeHistoryRepository.GetByServiceIdAsync(otherService.ServiceId);

                ServiceChangeHistory? correlatedChange = otherChanges
                    .Where(c => c.FieldName == "HealthStatus" 
                        && Math.Abs((c.ChangedAt - change.ChangedAt).TotalMinutes) <= 15)
                    .FirstOrDefault();

                if (correlatedChange != null)
                {
                    int timeDiffSeconds = (int)(correlatedChange.ChangedAt - change.ChangedAt).TotalSeconds;

                    correlatedServices.Add(new CorrelatedService
                    {
                        ServiceName = otherService.ServiceName,
                        HealthStatus = otherService.HealthStatus.ToString(),
                        StatusChangedAt = correlatedChange.ChangedAt,
                        CorrelationType = timeDiffSeconds > 0 ? "SequentialFailure" : "SimultaneousFailure",
                        TimeDifferenceSeconds = Math.Abs(timeDiffSeconds)
                    });
                }
            }
        }

        return correlatedServices.DistinctBy(c => c.ServiceName).Take(10).ToList();
    }

    /// <summary>
    /// Layer 5: Recent changes (configuration, metadata, etc.)
    /// </summary>
    private async Task<List<ServiceChange>> GatherRecentChangesAsync(Service service, DateTime since)
    {
        List<ServiceChangeHistory> changes = await changeHistoryRepository.GetByServiceIdAsync(service.ServiceId);

        return changes
            .Where(c => c.FieldName != "HealthStatus" && c.ChangedAt >= since) // Exclude health status changes (already in timeline)
            .OrderByDescending(c => c.ChangedAt)
            .Take(10)
            .Select(c => new ServiceChange
            {
                ChangedAt = c.ChangedAt,
                ChangeType = c.ChangeType,
                ChangeDescription = c.ChangeDescription ?? "No description",
                ChangedBy = c.ChangedBy
            })
            .ToList();
    }

    /// <summary>
    /// Calculate average recovery time from unhealthy/degraded to healthy
    /// </summary>
    private double CalculateAverageRecoveryTime(List<ServiceChangeHistory> healthChanges)
    {
        List<double> recoveryTimes = new List<double>();

        for (int i = 0; i < healthChanges.Count - 1; i++)
        {
            ServiceChangeHistory current = healthChanges[i];
            ServiceChangeHistory next = healthChanges[i + 1];

            if ((current.NewValue == "Unhealthy" || current.NewValue == "Degraded" || current.NewValue == "Dead")
                && (next.NewValue == "Healthy" || next.NewValue == "Recovered"))
            {
                double recoveryMinutes = (next.ChangedAt - current.ChangedAt).TotalMinutes;
                recoveryTimes.Add(recoveryMinutes);
            }
        }

        return recoveryTimes.Count > 0 ? recoveryTimes.Average() : 0;
    }

    /// <summary>
    /// Determine most frequent failure pattern
    /// </summary>
    private string DetermineFailurePattern(List<ServiceChangeHistory> healthChanges)
    {
        int healthyToDegraded = healthChanges.Count(c => c.OldValue == "Healthy" && c.NewValue == "Degraded");
        int healthyToUnhealthy = healthChanges.Count(c => c.OldValue == "Healthy" && c.NewValue == "Unhealthy");
        int unhealthyToDead = healthChanges.Count(c => c.OldValue == "Unhealthy" && c.NewValue == "Dead");

        if (healthyToDegraded > healthyToUnhealthy && healthyToDegraded > unhealthyToDead)
            return "Gradual degradation (Healthy → Degraded)";
        if (healthyToUnhealthy > unhealthyToDead)
            return "Sudden failures (Healthy → Unhealthy)";
        if (unhealthyToDead > 0)
            return "Progressive failures (Unhealthy → Dead)";

        return "Mixed patterns";
    }

    /// <summary>
    /// Estimate token count for context (rough approximation: 1 token ≈ 4 characters)
    /// </summary>
    private int EstimateTokenCount(AnalysisContext context)
    {
        int count = 0;

        // Layer 1: Service identity (~500 tokens)
        count += 500;

        // Layer 2: Health timeline (~40 tokens per event)
        count += context.HealthTimeline.Count * 40;

        // Layer 3: Historical patterns (~300 tokens)
        count += 300;

        // Layer 4: Correlated services (~100 tokens per service)
        count += context.CorrelatedServices.Count * 100;

        // Layer 5: Recent changes (~50 tokens per change)
        count += context.RecentChanges.Count * 50;

        return count;
    }
}
