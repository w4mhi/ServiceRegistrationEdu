using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;
using Omni.ServiceRegistry.Services.HealthInsights;

namespace Omni.ServiceRegistry.Api.Services;

/// <summary>
/// Background worker that processes health analysis queue continuously
/// </summary>
public class HealthInsightsWorker : BackgroundService
{
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly AnalysisQueueService queueService;
    private readonly ILogger<HealthInsightsWorker> logger;

    public HealthInsightsWorker(
        IServiceScopeFactory serviceScopeFactory,
        AnalysisQueueService queueService,
        ILogger<HealthInsightsWorker> logger)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.queueService = queueService;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Health Insights Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                AnalysisRequest? request = await queueService.DequeueAsync(stoppingToken);
                
                if (request == null)
                {
                    continue;
                }

                using IServiceScope scope = serviceScopeFactory.CreateScope();
                IHealthInsightsAnalysisService analysisService = scope.ServiceProvider.GetRequiredService<IHealthInsightsAnalysisService>();
                IHealthStatusNotifier notifier = scope.ServiceProvider.GetRequiredService<IHealthStatusNotifier>();

                logger.LogInformation("Processing analysis request {RequestId} for service {ServiceId}", 
                    request.RequestId, request.ServiceId);

                try
                {
                    await notifier.NotifyAnalysisStartedAsync(request.ServiceId);

                    ServiceHealthInsight insight = await analysisService.AnalyzeServiceAsync(
                        request.ServiceId,
                        request.TriggeredBy,
                        request.IsManualTrigger,
                        stoppingToken);

                    await notifier.NotifyAnalysisCompletedAsync(request.ServiceId, insight.InsightId);

                    logger.LogInformation("Analysis completed for service {ServiceId}: {Status}, {Tokens} tokens", 
                        request.ServiceId, insight.AnalysisStatus, insight.TokensUsed);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("noise reduction"))
                {
                    logger.LogInformation("Analysis skipped for service {ServiceId}: {Reason}", 
                        request.ServiceId, ex.Message);
                    
                    await notifier.NotifyAnalysisSkippedAsync(request.ServiceId, ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Analysis failed for service {ServiceId}", request.ServiceId);
                    
                    await notifier.NotifyAnalysisFailedAsync(request.ServiceId, ex.Message);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Health Insights Worker stopping");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in Health Insights Worker");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("Health Insights Worker stopped");
    }
}
