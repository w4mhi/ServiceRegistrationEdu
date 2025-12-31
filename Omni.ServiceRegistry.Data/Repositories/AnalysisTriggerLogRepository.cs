using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Omni.ServiceRegistry.Data.Postgres;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Data.Repositories;

/// <summary>
/// PostgreSQL implementation of AnalysisTriggerLog repository
/// </summary>
public class AnalysisTriggerLogRepository : IAnalysisTriggerLogRepository
{
    private readonly ServiceRegistryDbContext dbContext;

    public AnalysisTriggerLogRepository(ServiceRegistryDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<AnalysisTriggerLog?> GetLastTriggerAsync(string triggeredBy)
    {
        AnalysisTriggerLog? log = await dbContext.AnalysisTriggerLogs
            .Where(l => l.TriggeredBy == triggeredBy)
            .OrderByDescending(l => l.TriggeredAt)
            .FirstOrDefaultAsync();

        return log;
    }

    public async Task<List<AnalysisTriggerLog>> GetTriggersAsync(string triggeredBy, DateTime since)
    {
        List<AnalysisTriggerLog> logs = await dbContext.AnalysisTriggerLogs
            .Where(l => l.TriggeredBy == triggeredBy && l.TriggeredAt >= since)
            .OrderByDescending(l => l.TriggeredAt)
            .ToListAsync();

        return logs;
    }

    public async Task RecordTriggerAsync(AnalysisTriggerLog log)
    {
        dbContext.AnalysisTriggerLogs.Add(log);
        await dbContext.SaveChangesAsync();
    }

    public async Task CleanupOldLogsAsync(DateTime before)
    {
        List<AnalysisTriggerLog> oldLogs = await dbContext.AnalysisTriggerLogs
            .Where(l => l.TriggeredAt < before)
            .ToListAsync();

        if (oldLogs.Count > 0)
        {
            dbContext.AnalysisTriggerLogs.RemoveRange(oldLogs);
            await dbContext.SaveChangesAsync();
        }
    }
}
