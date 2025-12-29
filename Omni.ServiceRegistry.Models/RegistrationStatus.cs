using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Models;

/// <summary>
/// Status of a service registration request (R1-R7)
/// </summary>
public enum RegistrationStatus
{
    /// <summary>
    /// Registration submitted, awaiting administrator approval
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Registration approved by administrator
    /// </summary>
    Approved = 1,
    
    /// <summary>
    /// Registration denied by administrator
    /// </summary>
    Denied = 2
}
