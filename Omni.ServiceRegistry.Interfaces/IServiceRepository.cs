using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Domain-level repository for approved services
/// </summary>
public interface IServiceRepository
{
    Task<Service?> GetByIdAsync(Guid serviceId);
    Task<Service?> GetByNormalizedNameAsync(string normalizedName);
    Task<List<Service>> GetByHealthStatusAsync(HealthStatus status);
    Task<List<Service>> GetByDeletionStatusAsync(DeletionStatus status);
    Task<List<Service>> GetByOwnerAsync(string ownerEmail);
    Task<List<Service>> GetAllAsync();
    Task<List<Service>> GetAllActiveAsync();
    Task<Service> AddAsync(Service service);
    Task UpdateAsync(Service service);
    Task<bool> ExistsByNormalizedNameAsync(string normalizedName);
}
