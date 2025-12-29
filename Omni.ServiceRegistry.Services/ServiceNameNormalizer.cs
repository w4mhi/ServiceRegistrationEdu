using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Security.Cryptography;
using System.Text;
using Omni.ServiceRegistry.Interfaces;

namespace Omni.ServiceRegistry.Services;

/// <summary>
/// SHA256-based service name normalizer for uniqueness validation
/// </summary>
public class ServiceNameNormalizer : IServiceNameNormalizer
{
    public string Normalize(string serviceName)
    {
        ArgumentNullException.ThrowIfNull(serviceName);
        
        string lowercaseName = serviceName.ToLowerInvariant();
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(lowercaseName));
        return Convert.ToHexString(hashBytes);
    }
}
