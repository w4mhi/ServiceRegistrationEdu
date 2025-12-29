using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Service name normalization using SHA256 hash
/// </summary>
public interface IServiceNameNormalizer
{
    /// <summary>
    /// Convert service name to SHA256 hash for uniqueness validation
    /// </summary>
    string Normalize(string serviceName);
}
