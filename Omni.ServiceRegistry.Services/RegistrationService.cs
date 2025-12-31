using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Services;

/// <summary>
/// Registration submission and status query service (US1, US3)
/// </summary>
public class RegistrationService : IRegistrationService
{
    private readonly IRegistrationRepository registrationRepository;
    private readonly IServiceNameNormalizer nameNormalizer;
    private readonly IRegistrationValidator validator;
    private readonly ILogger<RegistrationService>? logger;

    public RegistrationService(
        IRegistrationRepository registrationRepository,
        IServiceNameNormalizer nameNormalizer,
        IRegistrationValidator validator,
        ILogger<RegistrationService>? logger = null)
    {
        this.registrationRepository = registrationRepository;
        this.nameNormalizer = nameNormalizer;
        this.validator = validator;
        this.logger = logger;
    }

    /// <summary>
    /// Submit new registration request with idempotency (R1-R10)
    /// </summary>
    public async Task<RegistrationRequest> SubmitRegistrationAsync(
        string serviceName,
        string description,
        string contactEmail,
        string endpointsJson,
        string? apiEndpointsJson,
        int heartbeatTimeout,
        int maxMissedHeartbeats)
    {
        // Validate all fields (R6)
        if (!validator.ValidateServiceName(serviceName, out string nameError))
        {
            throw new ArgumentException(nameError, nameof(serviceName));
        }

        if (!validator.ValidateEmail(contactEmail, out string emailError))
        {
            throw new ArgumentException(emailError, nameof(contactEmail));
        }

        if (!validator.ValidateEndpoints(endpointsJson, out string endpointsError))
        {
            throw new ArgumentException(endpointsError, nameof(endpointsJson));
        }

        if (!validator.ValidateHeartbeatTimeout(heartbeatTimeout, out string timeoutError))
        {
            throw new ArgumentException(timeoutError, nameof(heartbeatTimeout));
        }

        if (!validator.ValidateMaxMissedHeartbeats(maxMissedHeartbeats, out string missedError))
        {
            throw new ArgumentException(missedError, nameof(maxMissedHeartbeats));
        }

        // Normalize service name (R4)
        string normalizedName = nameNormalizer.Normalize(serviceName);

        // Check for existing pending registration (idempotency - R10)
        RegistrationRequest? existingRequest = await registrationRepository.GetByNormalizedNameAsync(normalizedName);
        if (existingRequest != null && existingRequest.Status == RegistrationStatus.Pending)
        {
            logger?.LogInformation(
                "Idempotent registration request for service {ServiceName} with existing registration {RegistrationId}",
                serviceName, existingRequest.RegistrationId);
            return existingRequest;
        }

        // Check for duplicate approved/denied registration (R5)
        // Return existing registration for idempotency (client can poll status)
        if (existingRequest != null)
        {
            logger?.LogInformation(
                "Returning existing registration {RegistrationId} for service {ServiceName} with status {Status}",
                existingRequest.RegistrationId, serviceName, existingRequest.Status);
            return existingRequest;
        }

        // Create new registration request
        RegistrationRequest request = new()
        {
            RegistrationId = Guid.NewGuid(),
            ServiceName = serviceName,
            ServiceNameNormalized = normalizedName,
            Description = description,
            ContactEmail = contactEmail,
            Endpoints = endpointsJson,
            ApiEndpoints = apiEndpointsJson,
            HeartbeatTimeout = heartbeatTimeout,
            MaxMissedHeartbeats = maxMissedHeartbeats,
            Status = RegistrationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        RegistrationRequest saved = await registrationRepository.AddAsync(request);

        logger?.LogInformation(
            "Registration submitted for service {ServiceName} with ID {RegistrationId}",
            serviceName, saved.RegistrationId);

        return saved;
    }

    /// <summary>
    /// Get registration status by ID (R9)
    /// </summary>
    public async Task<RegistrationRequest?> GetRegistrationStatusAsync(Guid registrationId)
    {
        RegistrationRequest? request = await registrationRepository.GetByIdAsync(registrationId);
        
        if (request != null)
        {
            logger?.LogInformation(
                "Registration status queried for {RegistrationId}: {Status}",
                registrationId, request.Status);
        }

        return request;
    }
}
