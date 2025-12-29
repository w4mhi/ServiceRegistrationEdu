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
/// PostgreSQL implementation of service repository
/// </summary>
public class PostgresServiceRepository : IServiceRepository
{
    private readonly ServiceRegistryDbContext context;

    public PostgresServiceRepository(ServiceRegistryDbContext context)
    {
        this.context = context;
    }

    public async Task<Service?> GetByIdAsync(Guid serviceId)
    {
        return await context.Services
            .FirstOrDefaultAsync(s => s.ServiceId == serviceId);
    }

    public async Task<Service?> GetByNormalizedNameAsync(string normalizedName)
    {
        return await context.Services
            .FirstOrDefaultAsync(s => s.ServiceNameNormalized == normalizedName);
    }

    public async Task<List<Service>> GetByHealthStatusAsync(HealthStatus status)
    {
        return await context.Services
            .Where(s => s.HealthStatus == status)
            .OrderBy(s => s.ServiceName)
            .ToListAsync();
    }

    public async Task<List<Service>> GetByDeletionStatusAsync(DeletionStatus status)
    {
        return await context.Services
            .IgnoreQueryFilters()
            .Where(s => s.DeletionStatus == status)
            .OrderBy(s => s.ServiceName)
            .ToListAsync();
    }

    public async Task<List<Service>> GetByOwnerAsync(string ownerEmail)
    {
        return await context.Services
            .Where(s => s.ContactEmail == ownerEmail)
            .OrderBy(s => s.ServiceName)
            .ToListAsync();
    }

    public async Task<List<Service>> GetAllAsync()
    {
        return await context.Services
            .OrderBy(s => s.ServiceName)
            .ToListAsync();
    }

    public async Task<List<Service>> GetAllActiveAsync()
    {
        return await context.Services
            .OrderBy(s => s.ServiceName)
            .ToListAsync();
    }

    public async Task<Service> AddAsync(Service service)
    {
        context.Services.Add(service);
        await context.SaveChangesAsync();
        return service;
    }

    public async Task UpdateAsync(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        
        service.UpdatedAt = DateTime.UtcNow;
        context.Services.Update(service);
        
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Handle optimistic concurrency conflicts
            throw new InvalidOperationException(
                "Service was modified by another process. Please reload and try again.");
        }
    }

    public async Task<bool> ExistsByNormalizedNameAsync(string normalizedName)
    {
        return await context.Services
            .AnyAsync(s => s.ServiceNameNormalized == normalizedName);
    }
}
