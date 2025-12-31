using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Omni.ServiceRegistry.Client.Models;

/// <summary>
/// OpenAPI 3.0 specification structure (simplified)
/// </summary>
public class OpenApiSpec
{
    [JsonPropertyName("openapi")]
    public string OpenApi { get; set; } = string.Empty;
    
    [JsonPropertyName("info")]
    public OpenApiInfo Info { get; set; } = new();
    
    [JsonPropertyName("paths")]
    public Dictionary<string, Dictionary<string, OpenApiOperation>> Paths { get; set; } = new();
}

public class OpenApiInfo
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class OpenApiOperation
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
    
    [JsonPropertyName("security")]
    public List<Dictionary<string, List<string>>>? Security { get; set; }
}
