using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using System.Net.Http.Json;
using System.Text.Json;
using Omni.ServiceRegistry.Dashboard.DTOs;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Dashboard.Services;

/// <summary>
/// HTTP client for communicating with the Service Registry API
/// </summary>
public class ServiceRegistryApiClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<ServiceRegistryApiClient> logger;

    public ServiceRegistryApiClient(HttpClient httpClient, ILogger<ServiceRegistryApiClient> logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Registration endpoints
    public async Task<List<RegistrationRequest>> GetAllRegistrationsAsync()
    {
        try
        {
            // Fetch all registrations (pending and denied) for the management page
            List<RegistrationRequestDto>? dtos = await httpClient.GetFromJsonAsync<List<RegistrationRequestDto>>(
                "/api/v1/admin/registrations/all");
            if (dtos == null) return new List<RegistrationRequest>();
            return dtos.Select(ConvertDtoToRegistrationRequest).ToList();
        }
#pragma warning disable CA1031 // Dashboard API client returns empty/default for all errors
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all registrations from API");
            // Fallback to pending only if /all endpoint doesn't exist
            try
            {
                List<RegistrationRequestDto>? dtos = await httpClient.GetFromJsonAsync<List<RegistrationRequestDto>>(
                    "/api/v1/admin/registrations/pending");
                if (dtos == null) return new List<RegistrationRequest>();
                return dtos.Select(ConvertDtoToRegistrationRequest).ToList();
            }
            catch
            {
                return new List<RegistrationRequest>();
            }
        }
#pragma warning restore CA1031
    }

    public async Task<bool> ApproveRegistrationAsync(Guid registrationId, string approvedBy, string? comments)
    {
        try
        {
            logger.LogInformation("Attempting to approve registration {RegistrationId}", registrationId);
            // Use property names matching ApprovalRequestDto (ApprovedBy, Comments)
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                $"/api/v1/admin/registrations/{registrationId}/approve",
                new { ApprovedBy = approvedBy, Comments = comments });
            logger.LogInformation("Approval response status: {StatusCode}", response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                logger.LogError("Approval failed: {StatusCode} - {Content}", response.StatusCode, content);
            }
            return response.IsSuccessStatusCode;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to approve registration {RegistrationId}", registrationId);
            return false;
        }
#pragma warning restore CA1031
    }

    public async Task<(bool Success, string? ErrorMessage)> DenyRegistrationAsync(Guid registrationId, string deniedBy, string comments)
    {
        try
        {
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                $"/api/v1/admin/registrations/{registrationId}/deny",
                new { DeniedBy = deniedBy, Comments = comments });
            
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError(
                    "Failed to deny registration {RegistrationId}. Status: {StatusCode}, Response: {Response}",
                    registrationId, response.StatusCode, errorContent);
                
                // Parse validation error message
                string errorMessage = "Failed to deny registration. Please try again.";
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(errorContent);
                    if (doc.RootElement.TryGetProperty("errors", out JsonElement errors))
                    {
                        // ASP.NET Core validation errors format
                        if (errors.TryGetProperty("Comments", out JsonElement commentsErrors))
                        {
                            JsonElement firstError = commentsErrors.EnumerateArray().First();
                            errorMessage = firstError.GetString() ?? errorMessage;
                        }
                    }
                    else if (doc.RootElement.TryGetProperty("title", out JsonElement title))
                    {
                        errorMessage = title.GetString() ?? errorMessage;
                    }
                }
                catch (JsonException)
                {
                    // If parsing fails, use the raw error content if it's short enough
                    if (errorContent.Length < 200)
                    {
                        errorMessage = errorContent;
                    }
                }
                
                return (false, errorMessage);
            }
            
            return (true, null);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deny registration {RegistrationId}", registrationId);
            return (false, $"Error: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    // Service endpoints
    public async Task<List<Service>> GetAllServicesAsync()
    {
        try
        {
            List<ServiceDto>? dtos = await httpClient.GetFromJsonAsync<List<ServiceDto>>("/api/v1/catalog");
            if (dtos == null) return new List<Service>();
            return dtos.Select(ConvertDtoToService).ToList();
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get services from API");
            return new List<Service>();
        }
#pragma warning restore CA1031
    }

    public async Task<Service?> GetServiceByIdAsync(Guid serviceId)
    {
        try
        {
            ServiceStatusResponseDto? dto = await httpClient.GetFromJsonAsync<ServiceStatusResponseDto>(
                $"/api/v1/status/service/{serviceId}");
            return dto != null ? ConvertStatusDtoToService(dto) : null;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get service {ServiceId} from API", serviceId);
            return null;
        }
#pragma warning restore CA1031
    }

    public async Task<List<Service>> GetServicesByHealthStatusAsync(HealthStatus status)
    {
        try
        {
            List<ServiceDto>? dtos = await httpClient.GetFromJsonAsync<List<ServiceDto>>(
                $"/api/v1/catalog?healthStatus={status}");
            if (dtos == null) return new List<Service>();
            return dtos.Select(ConvertDtoToService).ToList();
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get services by health status {Status} from API", status);
            return new List<Service>();
        }
#pragma warning restore CA1031
    }

    // Deletion endpoints
    public async Task<List<Service>> GetPendingDeletionsAsync()
    {
        try
        {
            List<Service>? result = await httpClient.GetFromJsonAsync<List<Service>>(
                "/api/v1/admin/deletions/pending");
            return result ?? new List<Service>();
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pending deletions from API");
            return new List<Service>();
        }
#pragma warning restore CA1031
    }

    public async Task<bool> RequestDeletionAsync(Guid serviceId, string requestedBy, string reason)
    {
        try
        {
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                $"/api/v1/delete/{serviceId}",
                new { requestedBy, reason });
            return response.IsSuccessStatusCode;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to request deletion for service {ServiceId}", serviceId);
            return false;
        }
#pragma warning restore CA1031
    }

    public async Task<bool> ApproveDeletionAsync(Guid serviceId, string approvedBy, string? comments)
    {
        try
        {
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                $"/api/v1/admin/deletions/{serviceId}/approve",
                new { approvedBy, comments });
            return response.IsSuccessStatusCode;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to approve deletion for service {ServiceId}", serviceId);
            return false;
        }
#pragma warning restore CA1031
    }

    // Helper method to convert DTO to domain model
    private static RegistrationRequest ConvertDtoToRegistrationRequest(RegistrationRequestDto dto)
    {
        return new RegistrationRequest
        {
            RegistrationId = dto.RegistrationId,
            ServiceName = dto.ServiceName,
            Description = dto.Description,
            ContactEmail = dto.ContactEmail,
            Endpoints = JsonSerializer.Serialize(dto.Endpoints), // Convert list to JSON string
            HeartbeatTimeout = dto.HeartbeatTimeout,
            MaxMissedHeartbeats = dto.MaxMissedHeartbeats,
            Status = dto.Status,
            ServiceId = null,
            ReviewedBy = null,
            ReviewedAt = dto.ReviewedAt,
            ReviewComments = dto.ReviewComments,
            CreatedAt = dto.CreatedAt,
            ServiceNameNormalized = string.Empty // Not needed for display
        };
    }

    private static Service ConvertDtoToService(ServiceDto dto)
    {
        return new Service
        {
            ServiceId = dto.ServiceId,
            ServiceName = dto.ServiceName,
            Description = dto.Description,
            ContactEmail = dto.ContactEmail,
            Endpoints = JsonSerializer.Serialize(dto.Endpoints),
            HealthStatus = Enum.Parse<HealthStatus>(dto.HealthStatus),
            LastHeartbeatTimestamp = dto.LastHeartbeatTimestamp,
            HeartbeatTimeout = dto.HeartbeatTimeout,
            MaxMissedHeartbeats = dto.MaxMissedHeartbeats,
            MissedHeartbeatCounter = dto.MissedHeartbeatCounter,
            HeartbeatCount = dto.HeartbeatCount,
            CreatedAt = dto.CreatedAt,
            ServiceNameNormalized = string.Empty, // Not needed for display
            DeletionStatus = DeletionStatus.Active,
            UpdatedAt = dto.CreatedAt
        };
    }

    private static Service ConvertStatusDtoToService(ServiceStatusResponseDto dto)
    {
        return new Service
        {
            ServiceId = dto.ServiceId,
            ServiceName = dto.ServiceName,
            Description = dto.Description,
            ContactEmail = dto.ContactEmail,
            Endpoints = JsonSerializer.Serialize(dto.Endpoints),
            HealthStatus = Enum.Parse<HealthStatus>(dto.HealthStatus),
            LastHeartbeatTimestamp = dto.LastHeartbeatTimestamp,
            HeartbeatTimeout = dto.HeartbeatTimeout,
            MaxMissedHeartbeats = dto.MaxMissedHeartbeats,
            MissedHeartbeatCounter = dto.MissedHeartbeatCounter,
            HeartbeatCount = dto.HeartbeatCount,
            CreatedAt = dto.CreatedAt,
            ServiceNameNormalized = string.Empty,
            DeletionStatus = DeletionStatus.Active,
            UpdatedAt = dto.UpdatedAt
        };
    }
}
