using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;
using Omni.ServiceRegistry.Services;
using Omni.ServiceRegistry.Tests.Common.Builders;
using Xunit;

namespace Omni.ServiceRegistry.Services.Tests.Unit;

public class ServiceCatalogServiceTests
{
    private readonly Mock<IServiceRepository> serviceRepository;
    private readonly Mock<ILogger<ServiceCatalogService>> logger;
    private readonly ServiceCatalogService service;

    public ServiceCatalogServiceTests()
    {
        serviceRepository = new Mock<IServiceRepository>();
        logger = new Mock<ILogger<ServiceCatalogService>>();
        service = new ServiceCatalogService(serviceRepository.Object, logger.Object);
    }

    [Fact]
    public async Task GetAllServicesAsync_ReturnsAllServices()
    {
        List<Service> services = new List<Service>
        {
            new ServiceBuilder().WithServiceName("catalog-service-1").Build(),
            new ServiceBuilder().WithServiceName("catalog-service-2").Build()
        };

        serviceRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(services);

        List<Service> result = await service.GetAllServicesAsync();

        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(services);
    }

    [Fact]
    public async Task SearchServicesAsync_WithEmptySearch_ReturnsAllServices()
    {
        List<Service> services = new List<Service>
        {
            new ServiceBuilder().WithServiceName("catalog-service-1").Build()
        };

        serviceRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(services);

        List<Service> result = await service.SearchServicesAsync(string.Empty);

        result.Should().BeEquivalentTo(services);
    }

    [Fact]
    public async Task SearchServicesAsync_FiltersByNameOrDescription()
    {
        Service matchingByName = new ServiceBuilder()
            .WithServiceName("orders-api")
            .WithDescription("Handles order processing")
            .Build();

        Service matchingByDescription = new ServiceBuilder()
            .WithServiceName("billing-svc")
            .WithDescription("Billing and payments")
            .Build();

        Service nonMatching = new ServiceBuilder()
            .WithServiceName("inventory-svc")
            .WithDescription("Inventory management")
            .Build();

        List<Service> allServices = new List<Service>
        {
            matchingByName,
            matchingByDescription,
            nonMatching
        };

        serviceRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(allServices);

        List<Service> result = await service.SearchServicesAsync("bill");

        result.Should().Contain(matchingByDescription);
        result.Should().NotContain(nonMatching);
    }

    [Fact]
    public async Task GetByOwnerAsync_DelegatesToRepository()
    {
        string ownerEmail = "owner@example.com";

        List<Service> services = new List<Service>
        {
            new ServiceBuilder().WithContactEmail(ownerEmail).Build()
        };

        serviceRepository
            .Setup(r => r.GetByOwnerAsync(ownerEmail))
            .ReturnsAsync(services);

        List<Service> result = await service.GetByOwnerAsync(ownerEmail);

        result.Should().BeEquivalentTo(services);
    }

    [Fact]
    public async Task GetByHealthStatusAsync_DelegatesToRepository()
    {
        HealthStatus status = HealthStatus.Degraded;

        List<Service> services = new List<Service>
        {
            new ServiceBuilder().WithHealthStatus(status).Build()
        };

        serviceRepository
            .Setup(r => r.GetByHealthStatusAsync(status))
            .ReturnsAsync(services);

        List<Service> result = await service.GetByHealthStatusAsync(status);

        result.Should().BeEquivalentTo(services);
    }

    [Fact]
    public async Task GetServiceByIdAsync_ReturnsService_WhenFound()
    {
        Guid serviceId = Guid.NewGuid();
        Service serviceEntity = new ServiceBuilder()
            .WithServiceId(serviceId)
            .WithServiceName("status-service")
            .Build();

        serviceRepository
            .Setup(r => r.GetByIdAsync(serviceId))
            .ReturnsAsync(serviceEntity);

        Service? result = await service.GetServiceByIdAsync(serviceId);

        result.Should().NotBeNull();
        result!.ServiceId.Should().Be(serviceId);
    }

    [Fact]
    public async Task GetServiceByIdAsync_ReturnsNull_WhenNotFound()
    {
        Guid serviceId = Guid.NewGuid();

        serviceRepository
            .Setup(r => r.GetByIdAsync(serviceId))
            .ReturnsAsync((Service?)null);

        Service? result = await service.GetServiceByIdAsync(serviceId);

        result.Should().BeNull();
    }
}
