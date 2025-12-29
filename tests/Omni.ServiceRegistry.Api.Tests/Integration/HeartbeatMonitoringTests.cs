using System.Net.Http;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Omni.ServiceRegistry.Api.DTOs;
using Xunit;

namespace Omni.ServiceRegistry.Api.Tests.Integration;

/// <summary>
/// Integration tests for heartbeat monitoring (US3, US5)
/// </summary>
public class HeartbeatMonitoringTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public HeartbeatMonitoringTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        
        this.factory = factory;
        this.client = factory.CreateClient();
    }

    private async Task<Guid> CreateAndApproveService(string serviceName)
    {
        // Register
        RegistrationRequestDto request = new()
        {
            ServiceName = serviceName,
            Description = "Test service",
            ContactEmail = "test@example.com",
            Endpoints = new List<string> { "http://localhost:8080" },
            HeartbeatTimeout = 5, // Short timeout for testing
            MaxMissedHeartbeats = 3
        };
        
        HttpResponseMessage registerResponse = await client.PostAsJsonAsync("/api/v1/register", request);
        RegistrationResponseDto? registration = await registerResponse.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(registration);

        // Approve
        ApprovalRequestDto approval = new()
        {
            ApprovedBy = "admin@test.com",
            Comments = "Approved for testing"
        };
        
        HttpResponseMessage approveResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/registrations/{registration.RegistrationId}/approve", 
            approval);
        
        RegistrationResponseDto? approvedRegistration = await approveResponse.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(approvedRegistration);
        Assert.NotNull(approvedRegistration.ServiceId);
        
        return approvedRegistration.ServiceId.Value;
    }

    [Fact]
    public async Task SendHeartbeat_ValidService_ReturnsSuccess()
    {
        // Arrange
        Guid serviceId = await CreateAndApproveService($"HeartbeatTest-{Guid.NewGuid()}");
        HeartbeatRequestDto heartbeat = new()
        {
            Metadata = new Dictionary<string, string>
            {
                { "version", "1.0.0" },
                { "host", "test-server" }
            }
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/v1/heartbeat/{serviceId}", heartbeat);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        HeartbeatResponseDto? result = await response.Content.ReadFromJsonAsync<HeartbeatResponseDto>();
        Assert.NotNull(result);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task SendHeartbeat_NonExistentService_ReturnsNotFound()
    {
        // Arrange
        Guid nonExistentId = Guid.NewGuid();
        HeartbeatRequestDto heartbeat = new()
        {
            Metadata = new Dictionary<string, string>()
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/v1/heartbeat/{nonExistentId}", heartbeat);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendHeartbeat_PerformanceTarget_CompletesUnder500ms()
    {
        // Arrange
        Guid serviceId = await CreateAndApproveService($"PerfHeartbeat-{Guid.NewGuid()}");
        HeartbeatRequestDto heartbeat = new()
        {
            Metadata = new Dictionary<string, string>()
        };

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/v1/heartbeat/{serviceId}", heartbeat);

        // Assert
        stopwatch.Stop();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 500, 
            $"Heartbeat took {stopwatch.ElapsedMilliseconds}ms, expected <500ms");
    }

    [Fact]
    public async Task GetServiceStatus_ExistingService_ReturnsStatus()
    {
        // Arrange
        Guid serviceId = await CreateAndApproveService($"StatusTest-{Guid.NewGuid()}");

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/v1/status/service/{serviceId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ServiceStatusResponseDto? status = await response.Content.ReadFromJsonAsync<ServiceStatusResponseDto>();
        Assert.NotNull(status);
        Assert.Equal(serviceId, status.ServiceId);
        Assert.Equal("Healthy", status.HealthStatus);
    }

    [Fact]
    public async Task GetServiceStatus_PerformanceTarget_CompletesUnder1Second()
    {
        // Arrange
        Guid serviceId = await CreateAndApproveService($"PerfStatus-{Guid.NewGuid()}");

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/v1/status/service/{serviceId}");

        // Assert
        stopwatch.Stop();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
            $"Status query took {stopwatch.ElapsedMilliseconds}ms, expected <1000ms");
    }

    [Fact]
    public async Task GetAllServices_ReturnsCatalog()
    {
        // Arrange - Create at least one service
        await CreateAndApproveService($"CatalogTest-{Guid.NewGuid()}");

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/catalog");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<ServiceDto>? catalog = await response.Content.ReadFromJsonAsync<List<ServiceDto>>();
        Assert.NotNull(catalog);
        Assert.NotEmpty(catalog);
    }

    [Fact]
    public async Task GetServicesByHealthStatus_FiltersCorrectly()
    {
        // Arrange
        await CreateAndApproveService($"FilterTest-{Guid.NewGuid()}");

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/catalog?healthStatus=Healthy");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<ServiceDto>? services = await response.Content.ReadFromJsonAsync<List<ServiceDto>>();
        Assert.NotNull(services);
        Assert.All(services, s => Assert.Equal("Healthy", s.HealthStatus));
    }
}
