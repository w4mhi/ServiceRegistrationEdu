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
/// PostgreSQL implementation of registration repository
/// </summary>
public class PostgresRegistrationRepository : IRegistrationRepository
{
    private readonly ServiceRegistryDbContext context;

    public PostgresRegistrationRepository(ServiceRegistryDbContext context)
    {
        this.context = context;
    }

    public async Task<RegistrationRequest?> GetByIdAsync(Guid registrationId)
    {
        return await context.RegistrationRequests
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId);
    }

    public async Task<RegistrationRequest?> GetByNormalizedNameAsync(string normalizedName)
    {
        return await context.RegistrationRequests
            .FirstOrDefaultAsync(r => r.ServiceNameNormalized == normalizedName);
    }

    public async Task<List<RegistrationRequest>> GetPendingAsync()
    {
        return await context.RegistrationRequests
            .Where(r => r.Status == RegistrationStatus.Pending || r.Status == RegistrationStatus.Denied)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<RegistrationRequest> AddAsync(RegistrationRequest request)
    {
        context.RegistrationRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
    }

    public async Task UpdateAsync(RegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        request.UpdatedAt = DateTime.UtcNow;
        context.RegistrationRequests.Update(request);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsByNormalizedNameAsync(string normalizedName)
    {
        return await context.RegistrationRequests
            .AnyAsync(r => r.ServiceNameNormalized == normalizedName);
    }
}
