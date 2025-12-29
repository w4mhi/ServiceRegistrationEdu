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
/// In-memory registration repository for testing (File provider)
/// </summary>
public class InMemoryRegistrationRepository : IRegistrationRepository
{
    private readonly ConcurrentDictionary<Guid, RegistrationRequest> registrations = new();
    private readonly ConcurrentDictionary<string, Guid> nameIndex = new();

    public Task<RegistrationRequest?> GetByIdAsync(Guid registrationId)
    {
        registrations.TryGetValue(registrationId, out RegistrationRequest? request);
        return Task.FromResult(request);
    }

    public Task<RegistrationRequest?> GetByNormalizedNameAsync(string normalizedName)
    {
        if (nameIndex.TryGetValue(normalizedName, out Guid registrationId))
        {
            registrations.TryGetValue(registrationId, out RegistrationRequest? request);
            return Task.FromResult(request);
        }
        return Task.FromResult<RegistrationRequest?>(null);
    }

    public Task<List<RegistrationRequest>> GetPendingAsync()
    {
        List<RegistrationRequest> pending = registrations.Values
            .Where(r => r.Status == RegistrationStatus.Pending || r.Status == RegistrationStatus.Denied)
            .OrderBy(r => r.CreatedAt)
            .ToList();
        return Task.FromResult(pending);
    }

    public Task<RegistrationRequest> AddAsync(RegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        registrations[request.RegistrationId] = request;
        nameIndex[request.ServiceNameNormalized] = request.RegistrationId;
        return Task.FromResult(request);
    }

    public Task UpdateAsync(RegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        request.UpdatedAt = DateTime.UtcNow;
        registrations[request.RegistrationId] = request;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsByNormalizedNameAsync(string normalizedName)
    {
        bool exists = nameIndex.ContainsKey(normalizedName);
        return Task.FromResult(exists);
    }
}
