using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Repository interface for ServiceDeletionCycle data access
/// </summary>
public interface IDeletionCycleRepository
{
    /// <summary>
    /// Get all deletion cycles for a specific service, ordered by cycle number
    /// </summary>
    Task<List<ServiceDeletionCycle>> GetByServiceIdAsync(Guid serviceId);
    
    /// <summary>
    /// Get a specific deletion cycle by its ID
    /// </summary>
    Task<ServiceDeletionCycle?> GetByIdAsync(Guid serviceDeletionCycleId);
    
    /// <summary>
    /// Get the most recent deletion cycle for a service
    /// </summary>
    Task<ServiceDeletionCycle?> GetLatestByServiceIdAsync(Guid serviceId);
    
    /// <summary>
    /// Create a new deletion cycle record
    /// </summary>
    Task<ServiceDeletionCycle> AddAsync(ServiceDeletionCycle cycle);
    
    /// <summary>
    /// Update an existing deletion cycle (for restoration tracking)
    /// </summary>
    Task UpdateAsync(ServiceDeletionCycle cycle);
    
    /// <summary>
    /// Get all services with pending restorations (deletion cycles with requested but not approved restoration)
    /// </summary>
    Task<List<ServiceDeletionCycle>> GetPendingRestorationsAsync();
    
    /// <summary>
    /// Get all deletion cycles across all services
    /// </summary>
    Task<List<ServiceDeletionCycle>> GetAllAsync();
}
