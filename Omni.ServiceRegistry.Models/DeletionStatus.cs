using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Models;

/// <summary>
/// Soft delete status for services (R31-R32)
/// </summary>
public enum DeletionStatus
{
    /// <summary>
    /// Service is active and operational
    /// </summary>
    Active = 0,
    
    /// <summary>
    /// Service deletion requested, awaiting approval
    /// </summary>
    PendingDeletion = 1,
    
    /// <summary>
    /// Service logically deleted (excluded from queries)
    /// </summary>
    Deleted = 2
}
