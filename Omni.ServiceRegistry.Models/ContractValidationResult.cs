using System;
using System.Collections.Generic;

namespace Omni.ServiceRegistry.Models;

/// <summary>
/// Result of contract validation for a registration request
/// </summary>
public class ContractValidationResult
{
    public ValidationStatus Status { get; set; }
    public DateTime ValidatedAt { get; set; }
    public List<EndpointCheckResult> EndpointChecks { get; set; } = new();
    public List<string> Failures { get; set; } = new();
    public int TotalEndpoints { get; set; }
    public int ReachableEndpoints { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Result of checking an individual endpoint
/// </summary>
public class EndpointCheckResult
{
    public string Endpoint { get; set; } = string.Empty;
    public bool IsReachable { get; set; }
    public int ResponseTimeMs { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
}
