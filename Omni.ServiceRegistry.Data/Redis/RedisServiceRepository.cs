using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Collections.Concurrent;
using System.Text.Json;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;
using StackExchange.Redis;

namespace Omni.ServiceRegistry.Data.Redis;

/// <summary>
/// Redis-based implementation of service repository using RedisJSON
/// </summary>
public class RedisServiceRepository : IServiceRepository
{
    private readonly IDatabase database;
    private const string ServiceKeyPrefix = "service:";
    private const string ServiceIndexKey = "services:all";

    public RedisServiceRepository(IConnectionMultiplexer redis)
    {
        ArgumentNullException.ThrowIfNull(redis);
        
        database = redis.GetDatabase();
    }

    public async Task<Service?> GetByIdAsync(Guid serviceId)
    {
        string key = GetServiceKey(serviceId);
        RedisValue value = await database.StringGetAsync(key);
        
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        Service? service = JsonSerializer.Deserialize<Service>(value.ToString());
        
        // Apply soft delete filter
        if (service?.DeletionStatus == DeletionStatus.Deleted)
        {
            return null;
        }

        return service;
    }

    public async Task<Service?> GetByNormalizedNameAsync(string normalizedName)
    {
        List<Service> allServices = await GetAllAsync();
        return allServices.FirstOrDefault(s => s.ServiceNameNormalized == normalizedName);
    }

    public async Task<List<Service>> GetByHealthStatusAsync(HealthStatus status)
    {
        List<Service> allServices = await GetAllAsync();
        return allServices.Where(s => s.HealthStatus == status).OrderBy(s => s.ServiceName).ToList();
    }

    public async Task<List<Service>> GetByDeletionStatusAsync(DeletionStatus status)
    {
        List<Service> allServices = await GetAllServicesIncludingDeletedAsync();
        return allServices.Where(s => s.DeletionStatus == status).OrderBy(s => s.ServiceName).ToList();
    }

    public async Task<List<Service>> GetByOwnerAsync(string ownerEmail)
    {
        List<Service> allServices = await GetAllAsync();
        return allServices.Where(s => s.ContactEmail == ownerEmail).OrderBy(s => s.ServiceName).ToList();
    }

    public async Task<List<Service>> GetAllAsync()
    {
        RedisValue[] serviceIds = await database.SetMembersAsync(ServiceIndexKey);
        List<Service> services = new();

        foreach (RedisValue serviceIdValue in serviceIds)
        {
            if (Guid.TryParse(serviceIdValue.ToString(), out Guid serviceId))
            {
                Service? service = await GetByIdAsync(serviceId);
                if (service != null)
                {
                    services.Add(service);
                }
            }
        }

        return services.OrderBy(s => s.ServiceName).ToList();
    }

    public async Task<List<Service>> GetAllActiveAsync()
    {
        return await GetAllAsync();
    }

    public async Task<Service> AddAsync(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        
        service.CreatedAt = DateTime.UtcNow;
        service.UpdatedAt = DateTime.UtcNow;

        string key = GetServiceKey(service.ServiceId);
        string json = JsonSerializer.Serialize(service);

        await database.StringSetAsync(key, json);
        await database.SetAddAsync(ServiceIndexKey, service.ServiceId.ToString());

        return service;
    }

    public async Task UpdateAsync(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        
        service.UpdatedAt = DateTime.UtcNow;

        string key = GetServiceKey(service.ServiceId);
        string json = JsonSerializer.Serialize(service);

        await database.StringSetAsync(key, json);
    }

    public async Task<bool> ExistsByNormalizedNameAsync(string normalizedName)
    {
        List<Service> allServices = await GetAllAsync();
        return allServices.Any(s => s.ServiceNameNormalized == normalizedName);
    }

    private static string GetServiceKey(Guid serviceId)
    {
        return $"{ServiceKeyPrefix}{serviceId}";
    }

    private async Task<List<Service>> GetAllServicesIncludingDeletedAsync()
    {
        RedisValue[] serviceIds = await database.SetMembersAsync(ServiceIndexKey);
        List<Service> services = new();

        foreach (RedisValue serviceIdValue in serviceIds)
        {
            if (Guid.TryParse(serviceIdValue.ToString(), out Guid serviceId))
            {
                string key = GetServiceKey(serviceId);
                RedisValue value = await database.StringGetAsync(key);
                
                if (!value.IsNullOrEmpty)
                {
                    Service? service = JsonSerializer.Deserialize<Service>(value.ToString());
                    if (service != null)
                    {
                        services.Add(service);
                    }
                }
            }
        }

        return services.OrderBy(s => s.ServiceName).ToList();
    }
}
