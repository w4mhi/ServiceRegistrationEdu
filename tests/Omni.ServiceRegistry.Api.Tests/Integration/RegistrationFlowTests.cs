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
/// Integration tests for registration flow (US1)
/// </summary>
public class RegistrationFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public RegistrationFlowTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        
        this.factory = factory;
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidRequest_ReturnsRegistrationId()
    {
        // Arrange
        RegistrationRequestDto request = new()
        {
            ServiceName = "TestService",
            Description = "Test service for integration testing",
            ContactEmail = "test@example.com",
            Endpoints = new List<string> { "http://localhost:8080" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        RegistrationResponseDto? result = await response.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.RegistrationId);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task Register_DuplicateServiceName_ReturnsExistingRegistrationId()
    {
        // Arrange
        RegistrationRequestDto request = new()
        {
            ServiceName = "DuplicateTest",
            Description = "Test duplicate detection",
            ContactEmail = "duplicate@example.com",
            Endpoints = new List<string> { "http://localhost:9000" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };

        // Act - Submit twice
        HttpResponseMessage response1 = await client.PostAsJsonAsync("/api/v1/register", request);
        HttpResponseMessage response2 = await client.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        
        RegistrationResponseDto? result1 = await response1.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        RegistrationResponseDto? result2 = await response2.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(result1.RegistrationId, result2.RegistrationId); // Same ID returned
    }

    [Fact]
    public async Task Register_InvalidServiceName_ReturnsBadRequest()
    {
        // Arrange
        RegistrationRequestDto request = new()
        {
            ServiceName = "ab", // Too short
            Description = "Invalid service name",
            ContactEmail = "invalid@example.com",
            Endpoints = new List<string> { "http://localhost:8080" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        RegistrationRequestDto request = new()
        {
            ServiceName = "ValidService",
            Description = "Invalid email test",
            ContactEmail = "not-an-email",
            Endpoints = new List<string> { "http://localhost:8080" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRegistrationStatus_ExistingRegistration_ReturnsStatus()
    {
        // Arrange - First create a registration
        RegistrationRequestDto request = new()
        {
            ServiceName = "StatusCheckService",
            Description = "Test status check",
            ContactEmail = "status@example.com",
            Endpoints = new List<string> { "http://localhost:8080" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };
        
        HttpResponseMessage registerResponse = await client.PostAsJsonAsync("/api/v1/register", request);
        RegistrationResponseDto? registration = await registerResponse.Content.ReadFromJsonAsync<RegistrationResponseDto>();
        Assert.NotNull(registration);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/v1/status/registration/{registration.RegistrationId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        RegistrationStatusResponseDto? status = await response.Content.ReadFromJsonAsync<RegistrationStatusResponseDto>();
        Assert.NotNull(status);
        Assert.Equal(registration.RegistrationId, status.RegistrationId);
        Assert.Equal("Pending", status.Status);
    }

    [Fact]
    public async Task GetRegistrationStatus_NonExistentRegistration_ReturnsNotFound()
    {
        // Arrange
        Guid nonExistentId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/v1/status/registration/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_PerformanceTarget_CompletesUnder2Seconds()
    {
        // Arrange
        RegistrationRequestDto request = new()
        {
            ServiceName = $"PerfTest-{Guid.NewGuid()}",
            Description = "Performance test",
            ContactEmail = "perf@example.com",
            Endpoints = new List<string> { "http://localhost:8080" },
            HeartbeatTimeout = 30,
            MaxMissedHeartbeats = 5
        };

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        stopwatch.Stop();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, 
            $"Registration took {stopwatch.ElapsedMilliseconds}ms, expected <2000ms");
    }
}
