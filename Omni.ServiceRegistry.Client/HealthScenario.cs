using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Client;

/// <summary>
/// Defines different health behavior scenarios for testing
/// </summary>
public enum HealthScenario
{
    /// <summary>
    /// Demo cycle: Send 3-7 heartbeats, miss 15-20 heartbeats (die), recover, repeat forever
    /// </summary>
    DemoCycle
}
