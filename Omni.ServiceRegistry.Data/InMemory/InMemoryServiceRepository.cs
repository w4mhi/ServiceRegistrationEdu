using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Collections.Concurrent;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Data.InMemory;

/// <summary>
/// In-memory implementation of service repository for testing
/// </summary>
public class InMemoryServiceRepository : IServiceRepository
{
    private readonly ConcurrentDictionary<Guid, Service> services = new();

    public Task<Service?> GetByIdAsync(Guid serviceId)
    {
        services.TryGetValue(serviceId, out Service? service);
        return Task.FromResult(service);
    }

    public Task<Service?> GetByNormalizedNameAsync(string normalizedName)
    {
        Service? service = services.Values
            .FirstOrDefault(s => s.ServiceNameNormalized == normalizedName && s.DeletionStatus != DeletionStatus.Deleted);
        return Task.FromResult(service);
    }

    public Task<List<Service>> GetByHealthStatusAsync(HealthStatus status)
    {
        List<Service> result = services.Values
            .Where(s => s.HealthStatus == status && s.DeletionStatus != DeletionStatus.Deleted)
            .OrderBy(s => s.ServiceName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<Service>> GetByDeletionStatusAsync(DeletionStatus status)
    {
        List<Service> result = services.Values
            .Where(s => s.DeletionStatus == status)
            .OrderBy(s => s.ServiceName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<Service>> GetByOwnerAsync(string ownerEmail)
    {
        List<Service> result = services.Values
            .Where(s => s.ContactEmail == ownerEmail && s.DeletionStatus != DeletionStatus.Deleted)
            .OrderBy(s => s.ServiceName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<Service>> GetAllAsync()
    {
        List<Service> result = services.Values
            .Where(s => s.DeletionStatus != DeletionStatus.Deleted)
            .OrderBy(s => s.ServiceName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<Service>> GetAllActiveAsync()
    {
        List<Service> result = services.Values
            .Where(s => s.DeletionStatus != DeletionStatus.Deleted)
            .OrderBy(s => s.ServiceName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<Service> AddAsync(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        
        service.CreatedAt = DateTime.UtcNow;
        service.UpdatedAt = DateTime.UtcNow;
        services[service.ServiceId] = service;
        return Task.FromResult(service);
    }

    public Task UpdateAsync(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        
        service.UpdatedAt = DateTime.UtcNow;
        services[service.ServiceId] = service;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsByNormalizedNameAsync(string normalizedName)
    {
        bool exists = services.Values
            .Any(s => s.ServiceNameNormalized == normalizedName && s.DeletionStatus != DeletionStatus.Deleted);
        return Task.FromResult(exists);
    }
}
