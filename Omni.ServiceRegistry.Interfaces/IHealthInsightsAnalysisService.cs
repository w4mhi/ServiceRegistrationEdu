using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Service for orchestrating AI-powered health analysis
/// </summary>
public interface IHealthInsightsAnalysisService
{
    /// <summary>
    /// Analyze a single service health status
    /// </summary>
    Task<ServiceHealthInsight> AnalyzeServiceAsync(
        Guid serviceId, 
        string triggeredBy, 
        bool isManualTrigger, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze multiple services in batch
    /// </summary>
    Task<List<ServiceHealthInsight>> AnalyzeMultipleServicesAsync(
        List<Guid> serviceIds, 
        string triggeredBy, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if analysis should be skipped (noise reduction)
    /// </summary>
    Task<bool> ShouldSkipAnalysisAsync(Guid serviceId, string reason);
}
