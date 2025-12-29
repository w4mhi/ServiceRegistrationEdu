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
/// Integration tests for admin approval workflow (US2)
/// </summary>
public class AdminWorkflowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public AdminWorkflowTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        
        this.factory = factory;
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPendingRegistrations_ReturnsRegistrations()
    {
        // Arrange - Create a pending registration
        RegistrationRequestDto request = new()
        {
            ServiceName = "PendingService123",
            Description = "Test pending registration",
            ContactEmail = "pending@example.com",
            Endpoints = new List<string> { "http://localhost:8080" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };
        await client.PostAsJsonAsync("/api/v1/register", request);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/registrations/pending");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<RegistrationRequestDto>? pending = await response.Content.ReadFromJsonAsync<List<RegistrationRequestDto>>();
        Assert.NotNull(pending);
        Assert.NotEmpty(pending);
    }

    [Fact]
    public async Task ApproveRegistration_ValidRequest_CreatesService()
    {
        // Arrange - Create a registration to approve
        RegistrationRequestDto request = new()
        {
            ServiceName = $"ApprovalTest-{Guid.NewGuid()}",
            Description = "Test approval workflow",
            ContactEmail = "approval@example.com",
            Endpoints = new List<string> { "http://localhost:8080" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };
        
        HttpResponseMessage registerResponse = await client.PostAsJsonAsync("/api/v1/register", request);
        RegistrationResponseDto? registration = await registerResponse.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(registration);

        ApprovalRequestDto approval = new()
        {
            ApprovedBy = "admin@test.com",
            Comments = "Approved for testing"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/admin/registrations/{registration.RegistrationId}/approve", 
            approval);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        RegistrationResponseDto? result = await response.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(result);
        Assert.Equal("Approved", result.Status);
        Assert.NotNull(result.ServiceId);
        Assert.NotEqual(Guid.Empty, result.ServiceId.Value);
    }

    [Fact]
    public async Task DenyRegistration_ValidRequest_UpdatesStatus()
    {
        // Arrange - Create a registration to deny
        RegistrationRequestDto request = new()
        {
            ServiceName = $"DenialTest-{Guid.NewGuid()}",
            Description = "Test denial workflow",
            ContactEmail = "denial@example.com",
            Endpoints = new List<string> { "http://localhost:8080" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };
        
        HttpResponseMessage registerResponse = await client.PostAsJsonAsync("/api/v1/register", request);
        RegistrationResponseDto? registration = await registerResponse.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(registration);

        DenialRequestDto denial = new()
        {
            DeniedBy = "admin@test.com",
            Comments = "Does not meet requirements"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/admin/registrations/{registration.RegistrationId}/deny", 
            denial);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        RegistrationResponseDto? result = await response.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(result);
        Assert.Equal("Denied", result.Status);
    }

    [Fact]
    public async Task DenyRegistration_MissingComments_ReturnsBadRequest()
    {
        // Arrange
        RegistrationRequestDto request = new()
        {
            ServiceName = "DenialNoComments123",
            Description = "Test denial without comments",
            ContactEmail = "denial2@example.com",
            Endpoints = new List<string> { "http://localhost:8080" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };
        
        HttpResponseMessage registerResponse = await client.PostAsJsonAsync("/api/v1/register", request);
        RegistrationResponseDto? registration = await registerResponse.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(registration);

        DenialRequestDto denial = new()
        {
            DeniedBy = "admin@test.com",
            Comments = "" // Empty comments
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/admin/registrations/{registration.RegistrationId}/deny", 
            denial);

        // Assert - Just check status code, not body (returns ProblemDetails, not RegistrationResponseDto)
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ApproveRegistration_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        Guid nonExistentId = Guid.NewGuid();
        ApprovalRequestDto approval = new()
        {
            ApprovedBy = "admin@test.com",
            Comments = "Test"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/admin/registrations/{nonExistentId}/approve", 
            approval);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
