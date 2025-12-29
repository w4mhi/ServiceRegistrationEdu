using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Administrator service for approval workflow (US2)
/// </summary>
public interface IAdministratorService
{
    /// <summary>
    /// Get all pending registration requests (R25)
    /// </summary>
    Task<List<RegistrationRequest>> GetPendingRegistrationsAsync();
    
    /// <summary>
    /// Get all registration requests (pending and denied) for management UI
    /// </summary>
    Task<List<RegistrationRequest>> GetAllRegistrationsAsync();
    
    /// <summary>
    /// Approve a registration request (R11-R12)
    /// </summary>
    Task<Service> ApproveRegistrationAsync(Guid registrationId, string approvedBy, string? comments = null);
    
    /// <summary>
    /// Deny a registration request (R12-R13)
    /// </summary>
    Task DenyRegistrationAsync(Guid registrationId, string deniedBy, string comments);
    
    /// <summary>
    /// Request service deletion (soft delete)
    /// </summary>
    Task RequestDeletionAsync(Guid serviceId, string requestedBy, string reason, string? comments = null);
    
    /// <summary>
    /// Get all pending deletion requests
    /// </summary>
    Task<List<Service>> GetPendingDeletionsAsync();
    
    /// <summary>
    /// Approve a deletion request (soft delete)
    /// </summary>
    Task ApproveDeletionAsync(Guid serviceId, string approvedBy);
}
