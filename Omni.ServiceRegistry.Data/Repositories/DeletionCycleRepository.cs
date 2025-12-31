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
/// Repository implementation for ServiceDeletionCycle data access
/// </summary>
public class DeletionCycleRepository : IDeletionCycleRepository
{
    private readonly ServiceRegistryDbContext dbContext;

    public DeletionCycleRepository(ServiceRegistryDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<List<ServiceDeletionCycle>> GetByServiceIdAsync(Guid serviceId)
    {
        List<ServiceDeletionCycle> cycles = await dbContext.ServiceDeletionCycles
            .Where(c => c.ServiceId == serviceId)
            .OrderBy(c => c.CycleNumber)
            .ToListAsync();
        
        return cycles;
    }

    public async Task<ServiceDeletionCycle?> GetByIdAsync(Guid serviceDeletionCycleId)
    {
        ServiceDeletionCycle? cycle = await dbContext.ServiceDeletionCycles
            .FirstOrDefaultAsync(c => c.ServiceDeletionCycleId == serviceDeletionCycleId);
        
        return cycle;
    }

    public async Task<ServiceDeletionCycle?> GetLatestByServiceIdAsync(Guid serviceId)
    {
        ServiceDeletionCycle? cycle = await dbContext.ServiceDeletionCycles
            .Where(c => c.ServiceId == serviceId)
            .OrderByDescending(c => c.CycleNumber)
            .FirstOrDefaultAsync();
        
        return cycle;
    }

    public async Task<ServiceDeletionCycle> AddAsync(ServiceDeletionCycle cycle)
    {
        dbContext.ServiceDeletionCycles.Add(cycle);
        await dbContext.SaveChangesAsync();
        return cycle;
    }

    public async Task UpdateAsync(ServiceDeletionCycle cycle)
    {
        dbContext.ServiceDeletionCycles.Update(cycle);
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<ServiceDeletionCycle>> GetPendingRestorationsAsync()
    {
        List<ServiceDeletionCycle> cycles = await dbContext.ServiceDeletionCycles
            .Where(c => c.RestorationRequestedAt != null && c.RestorationApprovedAt == null)
            .OrderBy(c => c.RestorationRequestedAt)
            .ToListAsync();
        
        return cycles;
    }

    public async Task<List<ServiceDeletionCycle>> GetAllAsync()
    {
        List<ServiceDeletionCycle> cycles = await dbContext.ServiceDeletionCycles
            .ToListAsync();
        
        return cycles;
    }
}
