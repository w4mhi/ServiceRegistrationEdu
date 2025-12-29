using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Data.Postgres.Repositories;

/// <summary>
/// PostgreSQL implementation of change history repository (deferred implementation)
/// </summary>
public class PostgresChangeHistoryRepository : IChangeHistoryRepository
{
    private readonly ServiceRegistryDbContext context;

    public PostgresChangeHistoryRepository(ServiceRegistryDbContext context)
    {
        this.context = context;
    }

    public async Task<List<ServiceChangeHistory>> GetByServiceIdAsync(Guid serviceId)
    {
        return await context.ServiceChangeHistory
            .Where(h => h.ServiceId == serviceId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
    }

    public async Task<ServiceChangeHistory> AddAsync(ServiceChangeHistory change)
    {
        context.ServiceChangeHistory.Add(change);
        await context.SaveChangesAsync();
        return change;
    }
}
