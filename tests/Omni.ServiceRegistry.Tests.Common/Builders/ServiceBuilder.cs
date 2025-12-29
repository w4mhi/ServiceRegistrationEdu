using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Tests.Common.Builders;

/// <summary>
/// Builder pattern for creating Service test data
/// </summary>
public class ServiceBuilder
{
    private Guid serviceId = Guid.NewGuid();
    private string serviceName = "test-service";
    private string description = "Test service for unit testing";
    private string contactEmail = "test@example.com";
    private string endpoints = "[\"http://localhost:8080\"]";
    private int heartbeatTimeout = 30;
    private int maxMissedHeartbeats = 5;
    private HealthStatus healthStatus = HealthStatus.Healthy;
    private int missedHeartbeatCounter = 0;
    private int heartbeatCount = 0;
    private DateTime? lastHeartbeatTimestamp = DateTime.UtcNow;
    private DateTime createdAt = DateTime.UtcNow.AddHours(-1);
    private DateTime updatedAt = DateTime.UtcNow;
    private DeletionStatus deletionStatus = DeletionStatus.Active;

    public ServiceBuilder WithServiceId(Guid id)
    {
        serviceId = id;
        return this;
    }

    public ServiceBuilder WithServiceName(string name)
    {
        serviceName = name;
        return this;
    }

    public ServiceBuilder WithDescription(string desc)
    {
        description = desc;
        return this;
    }

    public ServiceBuilder WithContactEmail(string email)
    {
        contactEmail = email;
        return this;
    }

    public ServiceBuilder WithEndpoints(string endpointsJson)
    {
        endpoints = endpointsJson;
        return this;
    }

    public ServiceBuilder WithHeartbeatTimeout(int timeout)
    {
        heartbeatTimeout = timeout;
        return this;
    }

    public ServiceBuilder WithMaxMissedHeartbeats(int max)
    {
        maxMissedHeartbeats = max;
        return this;
    }

    public ServiceBuilder WithHealthStatus(HealthStatus status)
    {
        healthStatus = status;
        return this;
    }

    public ServiceBuilder WithMissedHeartbeatCounter(int counter)
    {
        missedHeartbeatCounter = counter;
        return this;
    }

    public ServiceBuilder WithHeartbeatCount(int count)
    {
        heartbeatCount = count;
        return this;
    }

    public ServiceBuilder WithLastHeartbeatTimestamp(DateTime? timestamp)
    {
        lastHeartbeatTimestamp = timestamp;
        return this;
    }

    public ServiceBuilder AsUnhealthy()
    {
        healthStatus = HealthStatus.Unhealthy;
        missedHeartbeatCounter = 1;
        return this;
    }

    public ServiceBuilder AsDegraded()
    {
        healthStatus = HealthStatus.Degraded;
        missedHeartbeatCounter = 3;
        return this;
    }

    public ServiceBuilder AsDead()
    {
        healthStatus = HealthStatus.Dead;
        missedHeartbeatCounter = 6;
        lastHeartbeatTimestamp = DateTime.UtcNow.AddMinutes(-5);
        return this;
    }

    public ServiceBuilder AsDeleted()
    {
        deletionStatus = DeletionStatus.Deleted;
        return this;
    }

    public Service Build()
    {
        return new Service
        {
            ServiceId = serviceId,
            ServiceName = serviceName,
            Description = description,
            ContactEmail = contactEmail,
            Endpoints = endpoints,
            HeartbeatTimeout = heartbeatTimeout,
            MaxMissedHeartbeats = maxMissedHeartbeats,
            HealthStatus = healthStatus,
            MissedHeartbeatCounter = missedHeartbeatCounter,
            HeartbeatCount = heartbeatCount,
            LastHeartbeatTimestamp = lastHeartbeatTimestamp,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            DeletionStatus = deletionStatus
        };
    }
}
