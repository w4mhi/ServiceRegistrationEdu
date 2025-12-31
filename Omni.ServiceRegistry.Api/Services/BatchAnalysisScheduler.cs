using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;
using Omni.ServiceRegistry.Services.HealthInsights;

namespace Omni.ServiceRegistry.Api.Services;

/// <summary>
/// Scheduled task for batch health analysis (daily at midnight)
/// </summary>
public class BatchAnalysisScheduler : BackgroundService
{
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly AnalysisQueueService queueService;
    private readonly IConfiguration configuration;
    private readonly ILogger<BatchAnalysisScheduler> logger;

    public BatchAnalysisScheduler(
        IConfiguration configuration,
        IServiceScopeFactory serviceScopeFactory,
        AnalysisQueueService queueService,
        ILogger<BatchAnalysisScheduler> logger)
    {
        this.configuration = configuration;
        this.serviceScopeFactory = serviceScopeFactory;
        this.queueService = queueService;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool enabled = configuration.GetValue<bool>("HealthInsights:EnableAutomaticAIAnalysis", true);
        
        if (!enabled)
        {
            logger.LogInformation("Automatic AI Analysis is disabled via configuration");
            return;
        }
        
        logger.LogInformation("Batch Analysis Scheduler started (24-hour interval)");

        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime now = DateTime.UtcNow;
            DateTime nextRun = now.AddHours(24);
            TimeSpan delayUntilNextRun = nextRun - now;

            logger.LogInformation("Next batch analysis scheduled in {Hours} hours at {NextRun}", 
                delayUntilNextRun.TotalHours, nextRun);

            try
            {
                await Task.Delay(delayUntilNextRun, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Batch Analysis Scheduler stopping");
                break;
            }

            try
            {
                await RunBatchAnalysisAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Batch analysis failed");
            }
        }

        logger.LogInformation("Batch Analysis Scheduler stopped");
    }

    private async Task RunBatchAnalysisAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting scheduled batch analysis");

        using IServiceScope scope = serviceScopeFactory.CreateScope();
        IServiceRepository serviceRepository = scope.ServiceProvider.GetRequiredService<IServiceRepository>();

        List<Service> allServices = await serviceRepository.GetAllAsync();
        
        List<Service> unhealthyServices = allServices
            .Where(s => s.HealthStatus == HealthStatus.Unhealthy 
                     || s.HealthStatus == HealthStatus.Degraded 
                     || s.HealthStatus == HealthStatus.Dead)
            .ToList();

        logger.LogInformation("Found {Total} total services, {Unhealthy} unhealthy services", 
            allServices.Count, unhealthyServices.Count);

        if (unhealthyServices.Count == 0)
        {
            logger.LogInformation("No unhealthy services found, skipping batch analysis");
            return;
        }

        foreach (Service service in unhealthyServices)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            AnalysisRequest request = new AnalysisRequest
            {
                ServiceId = service.ServiceId,
                TriggeredBy = "BatchScheduler",
                IsManualTrigger = false,
                Priority = 0
            };

            await queueService.EnqueueAsync(request, cancellationToken);
        }

        logger.LogInformation("Queued {Count} unhealthy services for batch analysis", unhealthyServices.Count);
    }
}
