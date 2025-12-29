using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Validation service for registration requests (R6)
/// </summary>
public interface IRegistrationValidator
{
    /// <summary>
    /// Validate service name (3-50 chars, alphanumeric + hyphen)
    /// </summary>
    bool ValidateServiceName(string serviceName, out string errorMessage);
    
    /// <summary>
    /// Validate contact email format
    /// </summary>
    bool ValidateEmail(string email, out string errorMessage);
    
    /// <summary>
    /// Validate endpoints JSON array (1-10 valid URLs)
    /// </summary>
    bool ValidateEndpoints(string endpointsJson, out string errorMessage);
    
    /// <summary>
    /// Validate heartbeat timeout (positive integer, recommended 15-300 seconds)
    /// </summary>
    bool ValidateHeartbeatTimeout(int timeout, out string errorMessage);
    
    /// <summary>
    /// Validate max missed heartbeats (positive integer, recommended 3-20)
    /// </summary>
    bool ValidateMaxMissedHeartbeats(int maxMissed, out string errorMessage);
}
