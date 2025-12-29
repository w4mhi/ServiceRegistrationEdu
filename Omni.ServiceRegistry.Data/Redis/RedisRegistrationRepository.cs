using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Text.Json;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;
using StackExchange.Redis;

namespace Omni.ServiceRegistry.Data.Redis;

/// <summary>
/// Redis-based implementation of registration repository
/// </summary>
public class RedisRegistrationRepository : IRegistrationRepository
{
    private readonly IDatabase database;
    private const string RegistrationKeyPrefix = "registration:";
    private const string RegistrationIndexKey = "registrations:all";
    private const string PendingIndexKey = "registrations:pending";

    public RedisRegistrationRepository(IConnectionMultiplexer redis)
    {
        ArgumentNullException.ThrowIfNull(redis);
        
        database = redis.GetDatabase();
    }

    public async Task<RegistrationRequest?> GetByIdAsync(Guid registrationId)
    {
        string key = GetRegistrationKey(registrationId);
        RedisValue value = await database.StringGetAsync(key);
        
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<RegistrationRequest>(value.ToString());
    }

    public async Task<RegistrationRequest?> GetByNormalizedNameAsync(string normalizedName)
    {
        List<RegistrationRequest> allRegistrations = await GetAllRegistrationsAsync();
        return allRegistrations.FirstOrDefault(r => r.ServiceNameNormalized == normalizedName);
    }

    public async Task<RegistrationRequest?> GetByNormalizedNameAndStatusAsync(string normalizedName, RegistrationStatus status)
    {
        List<RegistrationRequest> allRegistrations = await GetAllRegistrationsAsync();
        return allRegistrations.FirstOrDefault(r => r.ServiceNameNormalized == normalizedName && r.Status == status);
    }

    public async Task<List<RegistrationRequest>> GetPendingAsync()
    {
        RedisValue[] registrationIds = await database.SetMembersAsync(PendingIndexKey);
        List<RegistrationRequest> registrations = new();

        foreach (RedisValue regIdValue in registrationIds)
        {
            if (Guid.TryParse(regIdValue.ToString(), out Guid regId))
            {
                RegistrationRequest? registration = await GetByIdAsync(regId);
                if (registration != null && (registration.Status == RegistrationStatus.Pending || registration.Status == RegistrationStatus.Denied))
                {
                    registrations.Add(registration);
                }
            }
        }

        return registrations.OrderBy(r => r.CreatedAt).ToList();
    }

    public async Task<RegistrationRequest> AddAsync(RegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        request.CreatedAt = DateTime.UtcNow;

        string key = GetRegistrationKey(request.RegistrationId);
        string json = JsonSerializer.Serialize(request);

        await database.StringSetAsync(key, json);
        await database.SetAddAsync(RegistrationIndexKey, request.RegistrationId.ToString());
        
        if (request.Status == RegistrationStatus.Pending)
        {
            await database.SetAddAsync(PendingIndexKey, request.RegistrationId.ToString());
        }

        return request;
    }

    public async Task UpdateAsync(RegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        string key = GetRegistrationKey(request.RegistrationId);
        string json = JsonSerializer.Serialize(request);

        await database.StringSetAsync(key, json);

        // Update pending index
        if (request.Status != RegistrationStatus.Pending)
        {
            await database.SetRemoveAsync(PendingIndexKey, request.RegistrationId.ToString());
        }
        else
        {
            await database.SetAddAsync(PendingIndexKey, request.RegistrationId.ToString());
        }
    }

    public async Task<bool> ExistsByNormalizedNameAsync(string normalizedName)
    {
        List<RegistrationRequest> allRegistrations = await GetAllRegistrationsAsync();
        return allRegistrations.Any(r => r.ServiceNameNormalized == normalizedName);
    }

    public async Task<bool> ExistsByNormalizedNameAndStatusAsync(string normalizedName, RegistrationStatus status)
    {
        List<RegistrationRequest> allRegistrations = await GetAllRegistrationsAsync();
        return allRegistrations.Any(r => r.ServiceNameNormalized == normalizedName && r.Status == status);
    }

    private static string GetRegistrationKey(Guid registrationId)
    {
        return $"{RegistrationKeyPrefix}{registrationId}";
    }

    private async Task<List<RegistrationRequest>> GetAllRegistrationsAsync()
    {
        RedisValue[] registrationIds = await database.SetMembersAsync(RegistrationIndexKey);
        List<RegistrationRequest> registrations = new();

        foreach (RedisValue regIdValue in registrationIds)
        {
            if (Guid.TryParse(regIdValue.ToString(), out Guid regId))
            {
                RegistrationRequest? registration = await GetByIdAsync(regId);
                if (registration != null)
                {
                    registrations.Add(registration);
                }
            }
        }

        return registrations;
    }
}
