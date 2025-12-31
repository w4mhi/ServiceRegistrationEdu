using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Data.Postgres;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Data.Repositories;

/// <summary>
/// PostgreSQL implementation of ServiceHealthInsight repository
/// </summary>
public class HealthInsightsRepository : IHealthInsightsRepository
{
    private readonly ServiceRegistryDbContext dbContext;
    private readonly ILogger<HealthInsightsRepository>? logger;

    public HealthInsightsRepository(ServiceRegistryDbContext dbContext, ILogger<HealthInsightsRepository>? logger = null)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    public async Task<ServiceHealthInsight?> GetLatestInsightAsync(Guid serviceId)
    {
        ServiceHealthInsight? insight = await dbContext.ServiceHealthInsights
            .Where(i => i.ServiceId == serviceId)
            .OrderByDescending(i => i.GeneratedAt)
            .FirstOrDefaultAsync();

        return insight;
    }

    public async Task<List<ServiceHealthInsight>> GetInsightsByServiceAsync(Guid serviceId, DateTime? since = null)
    {
        IQueryable<ServiceHealthInsight> query = dbContext.ServiceHealthInsights
            .Where(i => i.ServiceId == serviceId);

        if (since.HasValue)
        {
            query = query.Where(i => i.GeneratedAt >= since.Value);
        }

        List<ServiceHealthInsight> insights = await query
            .OrderByDescending(i => i.GeneratedAt)
            .ToListAsync();

        return insights;
    }

    public async Task<ServiceHealthInsight?> GetByIdAsync(Guid insightId)
    {
        ServiceHealthInsight? insight = await dbContext.ServiceHealthInsights
            .FirstOrDefaultAsync(i => i.InsightId == insightId);

        return insight;
    }

    public async Task<List<ServiceHealthInsight>> GetRecentInsightsAsync(int count, DateTime? since = null)
    {
        IQueryable<ServiceHealthInsight> query = dbContext.ServiceHealthInsights
            .IgnoreQueryFilters()
            .Where(i => i.AnalysisStatus == "Completed");

        if (since.HasValue)
        {
            query = query.Where(i => i.GeneratedAt >= since.Value);
        }

        List<ServiceHealthInsight> insights = await query
            .OrderByDescending(i => i.GeneratedAt)
            .Take(count)
            .ToListAsync();

        return insights;
    }

    public async Task<ServiceHealthInsight> AddInsightAsync(ServiceHealthInsight insight)
    {
        logger?.LogInformation("AddInsightAsync called for service {ServiceId}, Status: {Status}", insight.ServiceId, insight.AnalysisStatus);
        dbContext.ServiceHealthInsights.Add(insight);
        int changes = await dbContext.SaveChangesAsync();
        logger?.LogInformation("SaveChanges returned {Changes} rows affected, InsightId: {InsightId}", changes, insight.InsightId);
        return insight;
    }

    public async Task UpdateInsightAsync(ServiceHealthInsight insight)
    {
        logger?.LogInformation("UpdateInsightAsync called for insight {InsightId}, Status: {Status}", insight.InsightId, insight.AnalysisStatus);
        dbContext.ServiceHealthInsights.Update(insight);
        int changes = await dbContext.SaveChangesAsync();
        logger?.LogInformation("SaveChanges returned {Changes} rows affected for insight {InsightId}", changes, insight.InsightId);
    }

    public async Task<List<ServiceHealthInsight>> GetByStatusAsync(string status)
    {
        List<ServiceHealthInsight> insights = await dbContext.ServiceHealthInsights
            .Where(i => i.AnalysisStatus == status)
            .OrderBy(i => i.GeneratedAt)
            .ToListAsync();

        return insights;
    }
}
