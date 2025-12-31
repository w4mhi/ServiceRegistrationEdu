using System;
using System.Collections.Generic;

namespace Omni.ServiceRegistry.Dashboard.DTOs;

public class ValidationResultDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime ValidatedAt { get; set; }
    public List<EndpointCheckDto> EndpointChecks { get; set; } = new();
    public List<string> Failures { get; set; } = new();
    public int TotalEndpoints { get; set; }
    public int ReachableEndpoints { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class EndpointCheckDto
{
    public string Endpoint { get; set; } = string.Empty;
    public bool IsReachable { get; set; }
    public int ResponseTimeMs { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
}
