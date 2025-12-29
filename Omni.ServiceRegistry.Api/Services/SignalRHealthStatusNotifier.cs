using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Omni.ServiceRegistry.Api.Hubs;
using Omni.ServiceRegistry.Interfaces;

namespace Omni.ServiceRegistry.Api.Services;

/// <summary>
/// Implementation of IHealthStatusNotifier using SignalR hub context to broadcast events
/// </summary>
public class SignalRHealthStatusNotifier : IHealthStatusNotifier
{
    private readonly IHubContext<ServiceMonitorHub> hubContext;

    public SignalRHealthStatusNotifier(IHubContext<ServiceMonitorHub> hubContext)
    {
        this.hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }

    public async Task NotifyHealthStatusChangedAsync(
        Guid serviceId, 
        string serviceName,
        string previousStatus, 
        string newStatus,
        ILogger? logger = null)
    {
        logger?.LogTrace(
            "Broadcasting health status change for service {ServiceId}: {PreviousStatus} → {NewStatus}",
            serviceId, previousStatus, newStatus);
        
        await hubContext.Clients.All.SendAsync(
            "HealthStatusChanged",
            serviceId,
            serviceName,
            previousStatus,
            newStatus);
    }

    public async Task NotifyServiceAddedAsync(Guid serviceId, string serviceName, ILogger? logger = null)
    {
        logger?.LogTrace("Broadcasting service added: {ServiceId} - {ServiceName}", serviceId, serviceName);
        
        await hubContext.Clients.All.SendAsync(
            "ServiceAdded",
            serviceId,
            serviceName);
    }

    public async Task NotifyServiceRemovedAsync(Guid serviceId, string serviceName, ILogger? logger = null)
    {
        logger?.LogTrace("Broadcasting service removed: {ServiceId} - {ServiceName}", serviceId, serviceName);
        
        await hubContext.Clients.All.SendAsync(
            "ServiceRemoved",
            serviceId,
            serviceName);
    }

    public async Task NotifyRegistrationApprovedAsync(Guid registrationId, Guid serviceId, ILogger? logger = null)
    {
        logger?.LogTrace(
            "Broadcasting registration approved: {RegistrationId} → {ServiceId}",
            registrationId, serviceId);
        
        await hubContext.Clients.All.SendAsync(
            "RegistrationApproved",
            registrationId,
            serviceId);
    }

    public async Task NotifyRegistrationDeniedAsync(Guid registrationId, ILogger? logger = null)
    {
        logger?.LogTrace("Broadcasting registration denied: {RegistrationId}", registrationId);
        
        await hubContext.Clients.All.SendAsync(
            "RegistrationDenied",
            registrationId);
    }
}
