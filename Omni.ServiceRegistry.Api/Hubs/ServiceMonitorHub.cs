using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Api.Hubs;

/// <summary>
/// SignalR hub for broadcasting real-time service monitoring updates to connected clients.
/// </summary>
public class ServiceMonitorHub : Hub
{
    private readonly ILogger<ServiceMonitorHub> logger;

    public ServiceMonitorHub(ILogger<ServiceMonitorHub> logger)
    {
        this.logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation(
            "Client connected to ServiceMonitorHub. ConnectionId: {ConnectionId}",
            Context.ConnectionId);
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            logger.LogWarning(
                exception,
                "Client disconnected from ServiceMonitorHub with error. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            logger.LogInformation(
                "Client disconnected from ServiceMonitorHub. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}
