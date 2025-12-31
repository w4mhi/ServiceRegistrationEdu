using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Service catalog for browsing and searching registered services (US5)
/// </summary>
public interface IServiceCatalogService
{
    /// <summary>
    /// Get all active services (R14)
    /// </summary>
    Task<List<Service>> GetAllServicesAsync();
    
    /// <summary>
    /// Search services by name pattern (R15)
    /// </summary>
    Task<List<Service>> SearchServicesAsync(string searchTerm);
    
    /// <summary>
    /// Get services by owner email (R16)
    /// </summary>
    Task<List<Service>> GetByOwnerAsync(string ownerEmail);
    
    /// <summary>
    /// Get services by health status (R17)
    /// </summary>
    Task<List<Service>> GetByHealthStatusAsync(HealthStatus status);
    
    /// <summary>
    /// Get services by deletion status
    /// </summary>
    Task<List<Service>> GetServicesByDeletionStatusAsync(DeletionStatus status);
    
    /// <summary>
    /// Get service details by ID (R18)
    /// </summary>
    Task<Service?> GetServiceByIdAsync(Guid serviceId);
}
