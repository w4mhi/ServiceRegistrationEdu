using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Repository interface for AnalysisTriggerLog data access (cooldown enforcement)
/// </summary>
public interface IAnalysisTriggerLogRepository
{
    /// <summary>
    /// Get the most recent trigger log for an admin
    /// </summary>
    Task<AnalysisTriggerLog?> GetLastTriggerAsync(string triggeredBy);

    /// <summary>
    /// Get all trigger logs for an admin within a time window
    /// </summary>
    Task<List<AnalysisTriggerLog>> GetTriggersAsync(string triggeredBy, DateTime since);

    /// <summary>
    /// Record a new analysis trigger
    /// </summary>
    Task RecordTriggerAsync(AnalysisTriggerLog log);

    /// <summary>
    /// Clean up old trigger logs (beyond cooldown window)
    /// </summary>
    Task CleanupOldLogsAsync(DateTime before);
}
