using System;
using System.Collections.Generic;

namespace Omni.ServiceRegistry.Client.Models;

/// <summary>
/// Configuration for a simulated service
/// </summary>
public class ServiceConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public List<string> Endpoints { get; set; } = new();
    public int HeartbeatTimeout { get; set; }
    public int MaxMissedHeartbeats { get; set; }
    public string Scenario { get; set; } = string.Empty;
    public string OpenApiSpecPath { get; set; } = string.Empty;
}
