using System;

namespace Omni.ServiceRegistry.Models;

/// <summary>
/// API endpoint exposed by a service
/// </summary>
public class ApiEndpoint
{
    /// <summary>
    /// API path (e.g., /api/v1/payments)
    /// </summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, PATCH)
    /// </summary>
    public string Method { get; set; } = string.Empty;
    
    /// <summary>
    /// Brief description of API functionality
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// API version (e.g., v1, v2)
    /// </summary>
    public string Version { get; set; } = "v1";
    
    /// <summary>
    /// Tags/categories for grouping (e.g., Authentication, Data Access)
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Whether authentication is required
    /// </summary>
    public bool RequiresAuth { get; set; } = false;
}
