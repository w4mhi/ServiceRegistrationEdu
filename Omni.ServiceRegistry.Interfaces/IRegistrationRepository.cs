using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Domain-level repository for registration requests
/// </summary>
public interface IRegistrationRepository
{
    Task<RegistrationRequest?> GetByIdAsync(Guid registrationId);
    Task<RegistrationRequest?> GetByNormalizedNameAsync(string normalizedName);
    Task<List<RegistrationRequest>> GetPendingAsync();
    Task<RegistrationRequest> AddAsync(RegistrationRequest request);
    Task UpdateAsync(RegistrationRequest request);
    Task<bool> ExistsByNormalizedNameAsync(string normalizedName);
}
