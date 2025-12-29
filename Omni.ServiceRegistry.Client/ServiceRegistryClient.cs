using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Client.Models;

namespace Omni.ServiceRegistry.Client;

/// <summary>
/// Client for interacting with Service Registry API
/// </summary>
public class ServiceRegistryClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<ServiceRegistryClient> logger;
    private readonly string apiBaseUrl;

    public ServiceRegistryClient(
        HttpClient httpClient,
        ILogger<ServiceRegistryClient> logger,
        string apiBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(apiBaseUrl);
        
        this.httpClient = httpClient;
        this.logger = logger;
        this.apiBaseUrl = apiBaseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Register a new service
    /// </summary>
    public async Task<RegistrationResponseDto?> RegisterServiceAsync(RegistrationRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        try
        {
            string url = $"{apiBaseUrl}/api/v1/register";
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                RegistrationResponseDto? result = await response.Content.ReadFromJsonAsync<RegistrationResponseDto>();
                logger.LogInformation(
                    "Successfully registered service '{ServiceName}' with RegistrationId: {RegistrationId}",
                    request.ServiceName, result?.RegistrationId);
                return result;
            }

            string errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError(
                "Failed to register service '{ServiceName}'. Status: {StatusCode}, Error: {Error}",
                request.ServiceName, response.StatusCode, errorContent);
            return null;
        }
#pragma warning disable CA1031 // Catch all exceptions to return null for client resilience
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while registering service '{ServiceName}'", request.ServiceName);
            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Check registration status
    /// </summary>
    public async Task<RegistrationResponseDto?> GetRegistrationStatusAsync(Guid registrationId)
    {
        try
        {
            string url = $"{apiBaseUrl}/api/v1/status/registration/{registrationId}";
            HttpResponseMessage response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<RegistrationResponseDto>();
            }

            logger.LogWarning("Failed to get registration status. Status: {StatusCode}", response.StatusCode);
            return null;
        }
#pragma warning disable CA1031 // Catch all exceptions to return null for client resilience
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while checking registration status for {RegistrationId}", registrationId);
            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Send heartbeat
    /// </summary>
    public async Task<bool> SendHeartbeatAsync(Guid serviceId)
    {
        try
        {
            string url = $"{apiBaseUrl}/api/v1/heartbeat/{serviceId}";
            HeartbeatRequestDto request = new()
            {
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    { "version", "1.0.0" },
                    { "host", Environment.MachineName }
                }
            };

            HttpResponseMessage response = await httpClient.PostAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                logger.LogDebug("Heartbeat sent successfully for ServiceId: {ServiceId}", serviceId);
                return true;
            }

            logger.LogWarning(
                "Failed to send heartbeat for ServiceId: {ServiceId}. Status: {StatusCode}",
                serviceId, response.StatusCode);
            return false;
        }
#pragma warning disable CA1031 // Catch all exceptions to return false for client resilience
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while sending heartbeat for ServiceId: {ServiceId}", serviceId);
            return false;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Approve a registration (admin endpoint - for testing)
    /// </summary>
    public async Task<bool> ApproveRegistrationAsync(Guid registrationId, string approvedBy)
    {
        try
        {
            string url = $"{apiBaseUrl}/api/v1/admin/registrations/{registrationId}/approve";
            object request = new { ApprovedBy = approvedBy, Comments = "Auto-approved for testing" };

            HttpResponseMessage response = await httpClient.PostAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Registration {RegistrationId} approved", registrationId);
                return true;
            }

            string errorContent = await response.Content.ReadAsStringAsync();
            logger.LogWarning(
                "Failed to approve registration {RegistrationId}. Status: {StatusCode}, Error: {Error}",
                registrationId, response.StatusCode, errorContent);
            return false;
        }
#pragma warning disable CA1031 // Catch all exceptions to return false for client resilience
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while approving registration {RegistrationId}", registrationId);
            return false;
        }
#pragma warning restore CA1031
    }
}
