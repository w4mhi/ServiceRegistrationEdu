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

    public async Task<(bool Success, int SuccessCount, int FailureCount, string? ErrorMessage)> ApproveAllRegistrationsAsync(
        List<Guid> registrationIds, 
        string approvedBy, 
        string? comments)
    {
        try
        {
            logger.LogInformation("Attempting to approve {Count} registrations in bulk", registrationIds.Count);
            
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                "/api/v1/admin/registrations/approve-all",
                new 
                { 
                    RegistrationIds = registrationIds, 
                    ApprovedBy = approvedBy, 
                    Comments = comments 
                });
            
            if (!response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                logger.LogError("Bulk approval failed: {StatusCode} - {Content}", response.StatusCode, content);
                return (false, 0, 0, $"Bulk approval failed: {response.StatusCode}");
            }
            
            BulkApprovalResponseDto? result = await response.Content.ReadFromJsonAsync<BulkApprovalResponseDto>();
            
            if (result == null)
            {
                return (false, 0, 0, "Invalid response from server");
            }
            
            logger.LogInformation(
                "Bulk approval completed: {SuccessCount} successful, {FailureCount} failed",
                result.SuccessCount, result.FailureCount);
            
            return (true, result.SuccessCount, result.FailureCount, null);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to approve registrations in bulk");
            return (false, 0, 0, ex.Message);
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
            List<ServiceDto>? dtos = await httpClient.GetFromJsonAsync<List<ServiceDto>>(
                "/api/v1/admin/deletions/pending");
            if (dtos == null) return new List<Service>();
            
            return dtos.Select(dto => ConvertDtoToService(dto)).ToList();
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pending deletions from API");
            return new List<Service>();
        }
#pragma warning restore CA1031
    }

    public async Task<List<Service>> GetDeletedServicesAsync()
    {
        try
        {
            // Get deleted services from admin history endpoint
            List<ServiceDto>? dtos = await httpClient.GetFromJsonAsync<List<ServiceDto>>("/api/v1/admin/deletions/history");
            if (dtos == null) return new List<Service>();
            
            return dtos.Select(dto => ConvertDtoToService(dto)).ToList();
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get deleted services from API");
            return new List<Service>();
        }
#pragma warning restore CA1031
    }

    public async Task<bool> RequestDeletionAsync(Guid serviceId, string requestedBy, string reason, string? comments = null)
    {
        try
        {
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                $"/api/v1/admin/services/{serviceId}/delete",
                new { Reason = reason, Comments = comments });
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

    public async Task<bool> ApproveDeletionAsync(Guid serviceId, string approvedBy, string? reason)
    {
        try
        {
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                $"/api/v1/admin/deletions/{serviceId}/approve",
                new { Reason = reason });
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
    
    public async Task<(bool IsEligible, string? Reason, string? RestorationType, int? DaysUntilExpiration)> GetRestorationEligibilityAsync(Guid serviceId)
    {
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync($"/api/v1/admin/services/{serviceId}/restoration-eligibility");
            
            if (response.IsSuccessStatusCode)
            {
                using JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                JsonElement root = doc.RootElement;
                
                bool isEligible = root.GetProperty("isEligible").GetBoolean();
                
                string? reason = root.TryGetProperty("reason", out JsonElement reasonElement) && reasonElement.ValueKind != JsonValueKind.Null
                    ? reasonElement.GetString()
                    : null;
                    
                string? restorationType = root.TryGetProperty("restorationType", out JsonElement typeElement) && typeElement.ValueKind != JsonValueKind.Null
                    ? typeElement.GetString()
                    : null;
                    
                int? daysUntilExpiration = root.TryGetProperty("daysUntilExpiration", out JsonElement daysElement) && daysElement.ValueKind != JsonValueKind.Null
                    ? daysElement.GetInt32()
                    : null;
                
                return (isEligible, reason, restorationType, daysUntilExpiration);
            }
            
            return (false, "Unable to check eligibility", null, null);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check restoration eligibility for service {ServiceId}", serviceId);
            return (false, $"Error: {ex.Message}", null, null);
        }
#pragma warning restore CA1031
    }
    
    public async Task<bool> RestoreServiceQuicklyAsync(Guid serviceId, string restoredBy, string reason)
    {
        try
        {
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                $"/api/v1/admin/services/{serviceId}/quick-restore",
                new { RestoredBy = restoredBy, Reason = reason });
            return response.IsSuccessStatusCode;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to quick restore service {ServiceId}", serviceId);
            return false;
        }
#pragma warning restore CA1031
    }
    
    public async Task<bool> RestoreServiceFullyAsync(
        Guid serviceId, 
        string restoredBy, 
        string reason, 
        string? justification = null,
        bool ownerVerified = false,
        bool endpointsVerified = false)
    {
        try
        {
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                $"/api/v1/admin/services/{serviceId}/restore",
                new 
                { 
                    RestoredBy = restoredBy, 
                    Reason = reason,
                    Justification = justification,
                    OwnerVerified = ownerVerified,
                    EndpointsVerified = endpointsVerified
                });
            return response.IsSuccessStatusCode;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restore service {ServiceId}", serviceId);
            return false;
        }
#pragma warning restore CA1031
    }

    public async Task<List<ServiceDeletionCycleDto>> GetDeletionCyclesAsync(Guid serviceId)
    {
        try
        {
            List<ServiceDeletionCycleDto>? cycles = await httpClient.GetFromJsonAsync<List<ServiceDeletionCycleDto>>(
                $"/api/v1/admin/services/{serviceId}/deletion-cycles");
            return cycles ?? new List<ServiceDeletionCycleDto>();
        }
#pragma warning disable CA1031 // Dashboard API client returns empty/default for all errors
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get deletion cycles for service {ServiceId}", serviceId);
            return new List<ServiceDeletionCycleDto>();
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
            DeletionStatus = dto.DeletionStatus,
            UpdatedAt = dto.UpdatedAt,
            DeletionReason = dto.DeletionReason,
            DeletionApprovedBy = dto.DeletionApprovedBy,
            DeletionApprovedAt = dto.DeletionApprovedAt
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

    // Recently Restored Services endpoint
    public async Task<List<RecentlyRestoredServiceDto>> GetRecentlyRestoredServicesAsync()
    {
        try
        {
            List<RecentlyRestoredServiceDto>? dtos = await httpClient.GetFromJsonAsync<List<RecentlyRestoredServiceDto>>(
                "/api/v1/admin/services/recently-restored");
            return dtos ?? new List<RecentlyRestoredServiceDto>();
        }
#pragma warning disable CA1031 // Dashboard API client returns empty/default for all errors
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get recently restored services from API");
            return new List<RecentlyRestoredServiceDto>();
        }
#pragma warning restore CA1031
    }
}
