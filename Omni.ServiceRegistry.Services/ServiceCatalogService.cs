using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Services;

/// <summary>
/// Service catalog for browsing and searching registered services (US5, R14-R18)
/// </summary>
public class ServiceCatalogService : IServiceCatalogService
{
    private readonly IServiceRepository serviceRepository;
    private readonly ILogger<ServiceCatalogService>? logger;

    public ServiceCatalogService(
        IServiceRepository serviceRepository,
        ILogger<ServiceCatalogService>? logger = null)
    {
        this.serviceRepository = serviceRepository;
        this.logger = logger;
    }

    /// <summary>
    /// Get all active services (R14)
    /// </summary>
    public async Task<List<Service>> GetAllServicesAsync()
    {
        List<Service> services = await serviceRepository.GetAllAsync();
        
        logger?.LogInformation("Retrieved {Count} services from catalog", services.Count);
        
        return services;
    }

    /// <summary>
    /// Search services by name pattern (R15)
    /// </summary>
    public async Task<List<Service>> SearchServicesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllServicesAsync();
        }

        List<Service> allServices = await serviceRepository.GetAllAsync();
        List<Service> matches = allServices
            .Where(s => s.ServiceName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                       s.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();

        logger?.LogInformation(
            "Search for '{SearchTerm}' returned {Count} services",
            searchTerm, matches.Count);

        return matches;
    }

    /// <summary>
    /// Get services by owner email (R16)
    /// </summary>
    public async Task<List<Service>> GetByOwnerAsync(string ownerEmail)
    {
        List<Service> services = await serviceRepository.GetByOwnerAsync(ownerEmail);
        
        logger?.LogInformation(
            "Retrieved {Count} services for owner {OwnerEmail}",
            services.Count, ownerEmail);
        
        return services;
    }

    /// <summary>
    /// Get services by health status (R17)
    /// </summary>
    public async Task<List<Service>> GetByHealthStatusAsync(HealthStatus status)
    {
        List<Service> services = await serviceRepository.GetByHealthStatusAsync(status);
        
        logger?.LogInformation(
            "Retrieved {Count} services with health status {HealthStatus}",
            services.Count, status);
        
        return services;
    }

    /// <summary>
    /// Get service details by ID (R18)
    /// </summary>
    public async Task<Service?> GetServiceByIdAsync(Guid serviceId)
    {
        Service? service = await serviceRepository.GetByIdAsync(serviceId);
        
        if (service != null)
        {
            logger?.LogInformation("Service {ServiceId} retrieved: {ServiceName}", serviceId, service.ServiceName);
        }
        else
        {
            logger?.LogWarning("Service {ServiceId} not found", serviceId);
        }
        
        return service;
    }
}
