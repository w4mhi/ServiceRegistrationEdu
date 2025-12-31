using System;
using System.Threading.Tasks;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Service for validating that registered services meet contract requirements
/// </summary>
public interface IContractValidationService
{
    /// <summary>
    /// Validates a registration request by checking endpoints and API availability
    /// </summary>
    /// <param name="registrationId">Registration request to validate</param>
    /// <returns>Validation result with detailed checks</returns>
    Task<ContractValidationResult> ValidateServiceAsync(Guid registrationId);
}
