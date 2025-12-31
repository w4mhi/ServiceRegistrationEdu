using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Registration submission and status query service
/// </summary>
public interface IRegistrationService
{
    /// <summary>
    /// Submit new registration request (US1, R1-R10)
    /// </summary>
    Task<RegistrationRequest> SubmitRegistrationAsync(
        string serviceName,
        string description,
        string contactEmail,
        string endpointsJson,
        string? apiEndpointsJson,
        int heartbeatTimeout,
        int maxMissedHeartbeats);

    /// <summary>
    /// Get registration status by ID (US3, R9)
    /// </summary>
    Task<RegistrationRequest?> GetRegistrationStatusAsync(Guid registrationId);
}
