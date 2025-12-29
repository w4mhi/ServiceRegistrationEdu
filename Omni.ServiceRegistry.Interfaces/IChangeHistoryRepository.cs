using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Repository for service change history (audit log)
/// </summary>
public interface IChangeHistoryRepository
{
    Task<List<ServiceChangeHistory>> GetByServiceIdAsync(Guid serviceId);
    Task<ServiceChangeHistory> AddAsync(ServiceChangeHistory change);
}
