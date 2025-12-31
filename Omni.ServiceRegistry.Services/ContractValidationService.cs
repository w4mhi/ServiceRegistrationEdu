using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Services;

/// <summary>
/// Service for validating that registered services meet contract requirements
/// </summary>
public class ContractValidationService : IContractValidationService
{
    private readonly IRegistrationRepository registrationRepository;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<ContractValidationService> logger;

    public ContractValidationService(
        IRegistrationRepository registrationRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<ContractValidationService> logger)
    {
        this.registrationRepository = registrationRepository;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    public async Task<ContractValidationResult> ValidateServiceAsync(Guid registrationId)
    {
        logger.LogInformation("Starting contract validation for registration {RegistrationId}", registrationId);

        RegistrationRequest? registration = await registrationRepository.GetByIdAsync(registrationId);
        if (registration == null)
        {
            logger.LogWarning("Registration {RegistrationId} not found", registrationId);
            return new ContractValidationResult
            {
                Status = ValidationStatus.Failed,
                ValidatedAt = DateTime.UtcNow,
                Summary = "Registration not found",
                Failures = new List<string> { "Registration request does not exist" }
            };
        }

        ContractValidationResult result = new()
        {
            ValidatedAt = DateTime.UtcNow,
            EndpointChecks = new List<EndpointCheckResult>()
        };

        // Parse endpoints from JSON
        List<string> endpoints;
        try
        {
            endpoints = JsonSerializer.Deserialize<List<string>>(registration.Endpoints) ?? new List<string>();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse endpoints JSON for registration {RegistrationId}", registrationId);
            result.Status = ValidationStatus.Failed;
            result.Summary = "Invalid endpoints configuration";
            result.Failures.Add("Could not parse endpoints JSON");
            return result;
        }

        if (endpoints.Count == 0)
        {
            logger.LogWarning("Registration {RegistrationId} has no endpoints", registrationId);
            result.Status = ValidationStatus.Failed;
            result.Summary = "No endpoints registered";
            result.Failures.Add("Service must register at least one endpoint");
            return result;
        }

        result.TotalEndpoints = endpoints.Count;

        // Check each endpoint
        HttpClient httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(5);

        foreach (string endpoint in endpoints)
        {
            EndpointCheckResult checkResult = await CheckEndpointAsync(httpClient, endpoint);
            result.EndpointChecks.Add(checkResult);

            if (checkResult.IsReachable)
            {
                result.ReachableEndpoints++;
            }
            else
            {
                result.Failures.Add($"{endpoint}: {checkResult.Message}");
            }
        }

        // Calculate average response time
        if (result.EndpointChecks.Any(c => c.IsReachable))
        {
            result.AverageResponseTimeMs = result.EndpointChecks
                .Where(c => c.IsReachable)
                .Average(c => c.ResponseTimeMs);
        }

        // Determine overall validation status
        if (result.ReachableEndpoints == 0)
        {
            result.Status = ValidationStatus.Failed;
            result.Summary = $"All {result.TotalEndpoints} endpoints are unreachable";
        }
        else if (result.ReachableEndpoints < result.TotalEndpoints)
        {
            result.Status = ValidationStatus.Failed;
            result.Summary = $"Only {result.ReachableEndpoints} of {result.TotalEndpoints} endpoints are reachable";
        }
        else
        {
            result.Status = ValidationStatus.Passed;
            result.Summary = $"All {result.TotalEndpoints} endpoints validated successfully (avg {result.AverageResponseTimeMs:F0}ms)";
        }

        // Update registration with validation results
        registration.ValidationStatus = result.Status;
        registration.ValidationResults = JsonSerializer.Serialize(result);
        registration.LastValidatedAt = result.ValidatedAt;
        await registrationRepository.UpdateAsync(registration);

        logger.LogInformation(
            "Contract validation completed for registration {RegistrationId}: {Status} - {Summary}",
            registrationId, result.Status, result.Summary);

        return result;
    }

    private async Task<EndpointCheckResult> CheckEndpointAsync(HttpClient httpClient, string endpoint)
    {
        EndpointCheckResult result = new()
        {
            Endpoint = endpoint
        };

        try
        {
            // Try to append /health if endpoint doesn't have a path
            Uri uri = new(endpoint);
            string checkUrl = uri.PathAndQuery == "/" ? $"{endpoint.TrimEnd('/')}/health" : endpoint;

            Stopwatch stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response = await httpClient.GetAsync(checkUrl);
            stopwatch.Stop();

            result.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;
            result.StatusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                result.IsReachable = true;
                result.Message = $"OK ({result.StatusCode})";
                logger.LogInformation("Endpoint {Endpoint} validated successfully in {ResponseTime}ms", endpoint, result.ResponseTimeMs);
            }
            else
            {
                result.IsReachable = false;
                result.Message = $"HTTP {result.StatusCode}";
                logger.LogWarning("Endpoint {Endpoint} returned non-success status: {StatusCode}", endpoint, result.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            result.IsReachable = false;
            result.Message = $"Connection failed: {ex.Message}";
            logger.LogWarning(ex, "Failed to connect to endpoint {Endpoint}", endpoint);
        }
        catch (TaskCanceledException)
        {
            result.IsReachable = false;
            result.Message = "Timeout (5s)";
            logger.LogWarning("Endpoint {Endpoint} timed out after 5 seconds", endpoint);
        }
        catch (Exception ex)
        {
            result.IsReachable = false;
            result.Message = $"Error: {ex.Message}";
            logger.LogError(ex, "Unexpected error checking endpoint {Endpoint}", endpoint);
        }

        return result;
    }
}
