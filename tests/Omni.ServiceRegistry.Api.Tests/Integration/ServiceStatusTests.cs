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
/// Integration tests for service status and catalog endpoints (US5, R14-R18)
/// </summary>
public class ServiceStatusTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public ServiceStatusTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        this.factory = factory;
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task GetServiceStatus_ExistingService_ReturnsDetails()
    {
        RegistrationRequestDto registrationRequest = new RegistrationRequestDto
        {
            ServiceName = "CatalogStatusService",
            Description = "Service for status endpoint test",
            ContactEmail = "status-service@example.com",
            Endpoints = new List<string> { "http://localhost:8080" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };

        HttpResponseMessage registrationResponse = await client.PostAsJsonAsync("/api/v1/register", registrationRequest);
        RegistrationResponseDto? registration = await registrationResponse.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(registration);

        HttpResponseMessage approveResponse = await client.PostAsJsonAsync($"/api/v1/admin/registrations/{registration.RegistrationId}/approve", new ApprovalRequestDto
        {
            ApprovedBy = "admin@example.com",
            Comments = "Approved for status test"
        });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        RegistrationStatusResponseDto? statusResponse = await approveResponse.Content.ReadFromJsonAsync<RegistrationStatusResponseDto>();
        Assert.NotNull(statusResponse);
        Assert.NotNull(statusResponse!.ServiceId);

        HttpResponseMessage response = await client.GetAsync($"/api/v1/status/service/{statusResponse.ServiceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ServiceStatusResponseDto? serviceStatus = await response.Content.ReadFromJsonAsync<ServiceStatusResponseDto>();
        Assert.NotNull(serviceStatus);
        Assert.Equal(statusResponse.ServiceId, serviceStatus.ServiceId);
        Assert.Equal("CatalogStatusService", serviceStatus.ServiceName);
    }

    [Fact]
    public async Task GetServiceStatus_NonExistentService_ReturnsNotFound()
    {
        Guid nonExistentId = Guid.NewGuid();

        HttpResponseMessage response = await client.GetAsync($"/api/v1/status/service/{nonExistentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Catalog_GetAllServices_ReturnsApprovedServices()
    {
        RegistrationRequestDto request = new RegistrationRequestDto
        {
            ServiceName = "CatalogListService",
            Description = "Service for catalog list test",
            ContactEmail = "catalog@example.com",
            Endpoints = new List<string> { "http://localhost:8081" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };

        HttpResponseMessage registrationResponse = await client.PostAsJsonAsync("/api/v1/register", request);
        RegistrationResponseDto? registration = await registrationResponse.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(registration);

        HttpResponseMessage approveResponse = await client.PostAsJsonAsync($"/api/v1/admin/registrations/{registration.RegistrationId}/approve", new ApprovalRequestDto
        {
            ApprovedBy = "admin@example.com",
            Comments = "Approved for catalog list test"
        });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        HttpResponseMessage response = await client.GetAsync("/api/v1/catalog");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<ServiceDto>? services = await response.Content.ReadFromJsonAsync<List<ServiceDto>>();
        Assert.NotNull(services);
        Assert.Contains(services, s => s.ServiceName == "CatalogListService");
    }

    [Fact]
    public async Task Catalog_FilterByOwner_ReturnsOnlyMatchingServices()
    {
        RegistrationRequestDto request = new RegistrationRequestDto
        {
            ServiceName = "CatalogOwnerService",
            Description = "Service for owner filter test",
            ContactEmail = "owner-filter@example.com",
            Endpoints = new List<string> { "http://localhost:8082" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };

        HttpResponseMessage registrationResponse = await client.PostAsJsonAsync("/api/v1/register", request);
        RegistrationResponseDto? registration = await registrationResponse.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(registration);

        HttpResponseMessage approveResponse = await client.PostAsJsonAsync($"/api/v1/admin/registrations/{registration.RegistrationId}/approve", new ApprovalRequestDto
        {
            ApprovedBy = "admin@example.com",
            Comments = "Approved for owner filter test"
        });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        HttpResponseMessage response = await client.GetAsync("/api/v1/catalog?owner=owner-filter@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<ServiceDto>? services = await response.Content.ReadFromJsonAsync<List<ServiceDto>>();
        Assert.NotNull(services);
        Assert.All(services, s => Assert.Equal("owner-filter@example.com", s.ContactEmail));
    }
}
