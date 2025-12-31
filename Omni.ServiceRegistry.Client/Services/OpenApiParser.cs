using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Client.Models;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Client.Services;

/// <summary>
/// Parses OpenAPI specifications and extracts API endpoint information
/// </summary>
public class OpenApiParser
{
    private readonly ILogger<OpenApiParser> logger;

    public OpenApiParser(ILogger<OpenApiParser> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Parses OpenAPI spec file and extracts API endpoints
    /// </summary>
    public async Task<List<ApiEndpoint>> ParseSpecFileAsync(string specPath)
    {
        try
        {
            if (!File.Exists(specPath))
            {
                logger.LogWarning("OpenAPI spec file not found: {SpecPath}", specPath);
                return new List<ApiEndpoint>();
            }

            string jsonContent = await File.ReadAllTextAsync(specPath);
            OpenApiSpec? spec = JsonSerializer.Deserialize<OpenApiSpec>(jsonContent);

            if (spec == null || spec.Paths == null)
            {
                logger.LogWarning("Invalid OpenAPI spec format: {SpecPath}", specPath);
                return new List<ApiEndpoint>();
            }

            List<ApiEndpoint> endpoints = new();

            foreach (KeyValuePair<string, Dictionary<string, OpenApiOperation>> pathEntry in spec.Paths)
            {
                string path = pathEntry.Key;
                foreach (KeyValuePair<string, OpenApiOperation> methodEntry in pathEntry.Value)
                {
                    string method = methodEntry.Key.ToUpperInvariant();
                    OpenApiOperation operation = methodEntry.Value;

                    ApiEndpoint endpoint = new()
                    {
                        Path = path,
                        Method = method,
                        Description = operation.Description ?? operation.Summary ?? string.Empty,
                        Version = ExtractVersion(path),
                        Tags = operation.Tags.ToArray(),
                        RequiresAuth = operation.Security != null && operation.Security.Count > 0
                    };

                    endpoints.Add(endpoint);
                }
            }

            logger.LogInformation("Parsed {Count} API endpoints from {SpecPath}", endpoints.Count, specPath);
            return endpoints;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing OpenAPI spec: {SpecPath}", specPath);
            return new List<ApiEndpoint>();
        }
    }

    private string ExtractVersion(string path)
    {
        // Extract version from path like /api/v1/... or /api/v2/...
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            if (part.StartsWith("v", StringComparison.OrdinalIgnoreCase) && part.Length >= 2)
            {
                return part;
            }
        }
        return "v1";
    }
}
