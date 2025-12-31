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

namespace Omni.ServiceRegistry.Services;

/// <summary>
/// Background service that runs daily to clean up expired restoration badges
/// Runs at 2:00 AM UTC to remove badges from services that have exceeded display window
/// </summary>
public class RestorationBadgeCleanupService : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly IConfiguration configuration;
    private readonly ILogger<RestorationBadgeCleanupService> logger;
    private readonly TimeSpan checkInterval = TimeSpan.FromHours(1); // Check every hour

    public RestorationBadgeCleanupService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<RestorationBadgeCleanupService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.configuration = configuration;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Restoration Badge Cleanup Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                
                // Run cleanup at 2:00 AM UTC
                if (now.Hour == 2 && now.Minute < 60)
                {
                    await PerformCleanupAsync();
                    
                    // Sleep for 1 hour to avoid running multiple times in the same hour
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                else
                {
                    // Check again in 1 hour
                    await Task.Delay(checkInterval, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error in Restoration Badge Cleanup Service");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        logger.LogInformation("Restoration Badge Cleanup Service stopped");
    }

    private async Task PerformCleanupAsync()
    {
        using Microsoft.Extensions.DependencyInjection.IServiceScope scope = serviceProvider.CreateScope();
        IDeletionCycleRepository deletionCycleRepository = scope.ServiceProvider.GetRequiredService<IDeletionCycleRepository>();
        
        logger.LogInformation("Starting restoration badge cleanup");

        try
        {
            int quickRestoreBadgeDays = configuration.GetValue<int>("ServiceRestoration:QuickRestoreBadgeDisplayDays", 7);
            int fullRestoreBadgeDays = configuration.GetValue<int>("ServiceRestoration:FullRestoreBadgeDisplayDays", 14);
            
            List<ServiceDeletionCycle> allCycles = await deletionCycleRepository.GetAllAsync();
            DateTime now = DateTime.UtcNow;
            int cleanedCount = 0;

            // Group by ServiceId and get latest restoration for each service
            Dictionary<Guid, ServiceDeletionCycle> latestRestorations = allCycles
                .Where(c => c.RestorationApprovedAt.HasValue)
                .GroupBy(c => c.ServiceId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(c => c.RestorationApprovedAt).First()
                );

            foreach (KeyValuePair<Guid, ServiceDeletionCycle> kvp in latestRestorations)
            {
                ServiceDeletionCycle cycle = kvp.Value;
                if (!cycle.RestorationApprovedAt.HasValue) continue;

                TimeSpan timeSinceRestoration = now - cycle.RestorationApprovedAt.Value;
                int badgeDays = cycle.RestorationMethod == "Quick" ? quickRestoreBadgeDays : fullRestoreBadgeDays;

                if (timeSinceRestoration.TotalDays >= badgeDays)
                {
                    logger.LogInformation(
                        "Badge expired for service {ServiceId}: restored {Days} days ago via {Method} restore (threshold: {Threshold} days)",
                        cycle.ServiceId, (int)timeSinceRestoration.TotalDays, cycle.RestorationMethod, badgeDays);
                    cleanedCount++;
                }
            }

            logger.LogInformation(
                "Restoration badge cleanup completed. {Count} badges marked as expired",
                cleanedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during restoration badge cleanup");
        }
    }
}
