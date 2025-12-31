using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Text.Json;
using Omni.ServiceRegistry.Api.DTOs;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Api.Controllers;

/// <summary>
/// Service catalog endpoints for browsing and searching services (US5, R14-R17)
/// </summary>
[ApiController]
[Route("api/v1/catalog")]
public class CatalogController : ControllerBase
{
    private readonly IServiceCatalogService catalogService;
    private readonly ILogger<CatalogController> logger;

    public CatalogController(
        IServiceCatalogService catalogService,
        ILogger<CatalogController> logger)
    {
        this.catalogService = catalogService;
        this.logger = logger;
    }

    /// <summary>
    /// Get all services or search/filter by criteria (R14-R17)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ServiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ServiceDto>>> GetServices(
        [FromQuery] string? search = null,
        [FromQuery] string? owner = null,
        [FromQuery] string? healthStatus = null)
    {
        List<Service> services;

        if (!string.IsNullOrWhiteSpace(search))
        {
            services = await catalogService.SearchServicesAsync(search);
        }
        else if (!string.IsNullOrWhiteSpace(owner))
        {
            services = await catalogService.GetByOwnerAsync(owner);
        }
        else if (!string.IsNullOrWhiteSpace(healthStatus) && 
                 Enum.TryParse<HealthStatus>(healthStatus, ignoreCase: true, out HealthStatus status))
        {
            services = await catalogService.GetByHealthStatusAsync(status);
        }
        else
        {
            services = await catalogService.GetAllServicesAsync();
        }

        List<ServiceDto> dtos = services.Select(s => new ServiceDto
        {
            ServiceId = s.ServiceId,
            ServiceName = s.ServiceName,
            Description = s.Description,
            ContactEmail = s.ContactEmail,
            Endpoints = JsonSerializer.Deserialize<List<string>>(s.Endpoints) ?? new(),
            HealthStatus = s.HealthStatus.ToString(),
            LastHeartbeatTimestamp = s.LastHeartbeatTimestamp,
            HeartbeatTimeout = s.HeartbeatTimeout,
            MaxMissedHeartbeats = s.MaxMissedHeartbeats,
            MissedHeartbeatCounter = s.MissedHeartbeatCounter,
            HeartbeatCount = s.HeartbeatCount,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            DeletionStatus = s.DeletionStatus
        }).ToList();

        logger.LogInformation(
            "Catalog query returned {Count} services (search: {Search}, owner: {Owner}, status: {Status})",
            dtos.Count, search, owner, healthStatus);

        return Ok(dtos);
    }
}
