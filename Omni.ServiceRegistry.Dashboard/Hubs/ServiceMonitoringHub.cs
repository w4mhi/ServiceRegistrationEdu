using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;

namespace Omni.ServiceRegistry.Dashboard.Hubs;

/// <summary>
/// SignalR hub for real-time service health status updates
/// </summary>
public class ServiceMonitoringHub : Hub
{
    /// <summary>
    /// Send health status change to all connected clients
    /// </summary>
    public async Task NotifyHealthStatusChanged(Guid serviceId, string serviceName, string newStatus)
    {
        await Clients.All.SendAsync("HealthStatusChanged", serviceId, serviceName, newStatus);
    }
    
    /// <summary>
    /// Send service registration approved notification
    /// </summary>
    public async Task NotifyServiceRegistered(Guid serviceId, string serviceName)
    {
        await Clients.All.SendAsync("ServiceRegistered", serviceId, serviceName);
    }
    
    /// <summary>
    /// Send service deletion notification
    /// </summary>
    public async Task NotifyServiceDeleted(Guid serviceId, string serviceName)
    {
        await Clients.All.SendAsync("ServiceDeleted", serviceId, serviceName);
    }
}
