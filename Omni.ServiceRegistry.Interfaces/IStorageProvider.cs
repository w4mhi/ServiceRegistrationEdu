using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Interfaces;

/// <summary>
/// Low-level storage abstraction for pluggable backends
/// </summary>
public interface IStorageProvider
{
    /// <summary>
    /// Read entity by key
    /// </summary>
    Task<T?> ReadAsync<T>(string key) where T : class;
    
    /// <summary>
    /// Write entity to storage
    /// </summary>
    Task WriteAsync<T>(string key, T entity) where T : class;
    
    /// <summary>
    /// Delete entity by key
    /// </summary>
    Task<bool> DeleteAsync(string key);
    
    /// <summary>
    /// Query entities matching predicate
    /// </summary>
    Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate) where T : class;
    
    /// <summary>
    /// Check if entity exists
    /// </summary>
    Task<bool> ExistsAsync(string key);
}
