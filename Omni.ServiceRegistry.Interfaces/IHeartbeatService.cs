using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Service for processing heartbeat signals from registered services
/// </summary>
public interface IHeartbeatService
{
    /// <summary>
    /// Processes a heartbeat signal from a service and updates health status
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service sending the heartbeat</param>
    /// <param name="metadata">Optional metadata from the heartbeat request</param>
    /// <param name="logger">Logger instance for diagnostic output</param>
    /// <returns>Task representing the asynchronous operation with timestamp of processed heartbeat</returns>
    Task<DateTime> ProcessHeartbeatAsync(Guid serviceId, Dictionary<string, string>? metadata, ILogger? logger = null);
}
