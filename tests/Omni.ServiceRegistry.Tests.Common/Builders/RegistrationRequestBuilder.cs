using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Tests.Common.Builders;

/// <summary>
/// Builder pattern for creating RegistrationRequest test data
/// </summary>
public class RegistrationRequestBuilder
{
    private Guid registrationId = Guid.NewGuid();
    private string serviceName = "test-service";
    private string serviceNameNormalized = string.Empty;
    private string description = "Test service for unit testing";
    private string contactEmail = "test@example.com";
    private string endpoints = "[\"http://localhost:8080\"]";
    private int heartbeatTimeout = 30;
    private int maxMissedHeartbeats = 5;
    private RegistrationStatus status = RegistrationStatus.Pending;
    private Guid? serviceId = null;
    private string? reviewedBy = null;
    private DateTime? reviewedAt = null;
    private string? reviewComments = null;
    private DateTime createdAt = DateTime.UtcNow;
    private DateTime updatedAt = DateTime.UtcNow;

    public RegistrationRequestBuilder WithRegistrationId(Guid id)
    {
        registrationId = id;
        return this;
    }

    public RegistrationRequestBuilder WithServiceName(string name)
    {
        serviceName = name;
        return this;
    }

    public RegistrationRequestBuilder WithServiceNameNormalized(string normalized)
    {
        serviceNameNormalized = normalized;
        return this;
    }

    public RegistrationRequestBuilder WithDescription(string desc)
    {
        description = desc;
        return this;
    }

    public RegistrationRequestBuilder WithContactEmail(string email)
    {
        contactEmail = email;
        return this;
    }

    public RegistrationRequestBuilder WithEndpoints(string endpointsJson)
    {
        endpoints = endpointsJson;
        return this;
    }

    public RegistrationRequestBuilder WithHeartbeatTimeout(int timeout)
    {
        heartbeatTimeout = timeout;
        return this;
    }

    public RegistrationRequestBuilder WithMaxMissedHeartbeats(int max)
    {
        maxMissedHeartbeats = max;
        return this;
    }

    public RegistrationRequestBuilder WithStatus(RegistrationStatus registrationStatus)
    {
        status = registrationStatus;
        return this;
    }

    public RegistrationRequestBuilder WithServiceId(Guid? id)
    {
        serviceId = id;
        return this;
    }

    public RegistrationRequestBuilder AsApproved(string approver = "admin@example.com")
    {
        status = RegistrationStatus.Approved;
        reviewedBy = approver;
        reviewedAt = DateTime.UtcNow;
        reviewComments = "Approved for testing";
        serviceId = Guid.NewGuid();
        return this;
    }

    public RegistrationRequestBuilder AsDenied(string denier = "admin@example.com", string comments = "Does not meet requirements")
    {
        status = RegistrationStatus.Denied;
        reviewedBy = denier;
        reviewedAt = DateTime.UtcNow;
        reviewComments = comments;
        return this;
    }

    public RegistrationRequest Build()
    {
        return new RegistrationRequest
        {
            RegistrationId = registrationId,
            ServiceName = serviceName,
            ServiceNameNormalized = string.IsNullOrEmpty(serviceNameNormalized) 
                ? ComputeHash(serviceName) 
                : serviceNameNormalized,
            Description = description,
            ContactEmail = contactEmail,
            Endpoints = endpoints,
            HeartbeatTimeout = heartbeatTimeout,
            MaxMissedHeartbeats = maxMissedHeartbeats,
            Status = status,
            ServiceId = serviceId,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt,
            ReviewComments = reviewComments,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    private static string ComputeHash(string input)
    {
        using System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
        return Convert.ToHexString(hashBytes);
    }
}
