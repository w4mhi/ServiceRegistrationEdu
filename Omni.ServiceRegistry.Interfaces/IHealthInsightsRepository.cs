using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Repository interface for ServiceHealthInsight data access
/// </summary>
public interface IHealthInsightsRepository
{
    /// <summary>
    /// Get the latest insight for a specific service
    /// </summary>
    Task<ServiceHealthInsight?> GetLatestInsightAsync(Guid serviceId);

    /// <summary>
    /// Get all insights for a service, optionally filtered by date
    /// </summary>
    Task<List<ServiceHealthInsight>> GetInsightsByServiceAsync(Guid serviceId, DateTime? since = null);

    /// <summary>
    /// Get a specific insight by ID
    /// </summary>
    Task<ServiceHealthInsight?> GetByIdAsync(Guid insightId);

    /// <summary>
    /// Get recent insights across all services
    /// </summary>
    Task<List<ServiceHealthInsight>> GetRecentInsightsAsync(int count, DateTime? since = null);

    /// <summary>
    /// Add a new insight to the database
    /// </summary>
    Task<ServiceHealthInsight> AddInsightAsync(ServiceHealthInsight insight);

    /// <summary>
    /// Update an existing insight (e.g., status change, error message)
    /// </summary>
    Task UpdateInsightAsync(ServiceHealthInsight insight);

    /// <summary>
    /// Get insights by analysis status
    /// </summary>
    Task<List<ServiceHealthInsight>> GetByStatusAsync(string status);
}
