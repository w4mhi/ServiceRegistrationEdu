using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Client;

namespace Omni.ServiceRegistry.Dashboard.Services;

/// <summary>
/// SignalR hub client for receiving real-time updates from the API
/// </summary>
public class SignalRHubClient : IAsyncDisposable
{
    private readonly HubConnection hubConnection;
    private readonly ILogger<SignalRHubClient> logger;

    public SignalRHubClient(string hubUrl, ILogger<SignalRHubClient> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();
        
        hubConnection.Reconnecting += OnReconnecting;
        hubConnection.Reconnected += OnReconnected;
        hubConnection.Closed += OnClosed;
    }

    /// <summary>
    /// Start the connection to the SignalR hub
    /// </summary>
    public async Task StartAsync()
    {
        try
        {
            // Only start if not already connected
            if (hubConnection.State == HubConnectionState.Disconnected)
            {
                await hubConnection.StartAsync();
                logger.LogInformation("SignalR connection started");
            }
            else
            {
                logger.LogInformation("SignalR connection already in state: {State}", hubConnection.State);
            }
        }
#pragma warning disable CA1031 // Suppress for error handling in startup method
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting SignalR connection");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Stop the connection to the SignalR hub
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            await hubConnection.StopAsync();
            logger.LogInformation("SignalR connection stopped");
        }
#pragma warning disable CA1031 // Suppress for error handling in cleanup method
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping SignalR connection");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Subscribe to health status change events
    /// </summary>
    public void OnHealthStatusChanged(Func<Guid, string, string, string, Task> handler)
    {
        hubConnection.On<Guid, string, string, string>("HealthStatusChanged", handler);
        logger.LogTrace("Subscribed to HealthStatusChanged events");
    }

    /// <summary>
    /// Subscribe to service added events
    /// </summary>
    public void OnServiceAdded(Func<Guid, string, Task> handler)
    {
        hubConnection.On<Guid, string>("ServiceAdded", handler);
        logger.LogTrace("Subscribed to ServiceAdded events");
    }

    /// <summary>
    /// Subscribe to service removed events
    /// </summary>
    public void OnServiceRemoved(Func<Guid, string, Task> handler)
    {
        hubConnection.On<Guid, string>("ServiceRemoved", handler);
        logger.LogTrace("Subscribed to ServiceRemoved events");
    }

    /// <summary>
    /// Subscribe to registration approved events
    /// </summary>
    public void OnRegistrationApproved(Func<Guid, Guid, Task> handler)
    {
        hubConnection.On<Guid, Guid>("RegistrationApproved", handler);
        logger.LogTrace("Subscribed to RegistrationApproved events");
    }

    /// <summary>
    /// Subscribe to registration denied events
    /// </summary>
    public void OnRegistrationDenied(Func<Guid, Task> handler)
    {
        hubConnection.On<Guid>("RegistrationDenied", handler);
        logger.LogTrace("Subscribed to RegistrationDenied events");
    }

    /// <summary>
    /// Subscribe to analysis started events
    /// </summary>
    public void OnAnalysisStarted(Func<Guid, DateTime, Task> handler)
    {
        hubConnection.On<Guid, DateTime>("AnalysisStarted", handler);
        logger.LogTrace("Subscribed to AnalysisStarted events");
    }

    /// <summary>
    /// Subscribe to analysis completed events
    /// </summary>
    public void OnAnalysisCompleted(Func<Guid, Guid, DateTime, Task> handler)
    {
        hubConnection.On<Guid, Guid, DateTime>("AnalysisCompleted", handler);
        logger.LogTrace("Subscribed to AnalysisCompleted events");
    }

    /// <summary>
    /// Subscribe to analysis skipped events
    /// </summary>
    public void OnAnalysisSkipped(Func<Guid, string, DateTime, Task> handler)
    {
        hubConnection.On<Guid, string, DateTime>("AnalysisSkipped", handler);
        logger.LogTrace("Subscribed to AnalysisSkipped events");
    }

    /// <summary>
    /// Subscribe to analysis failed events
    /// </summary>
    public void OnAnalysisFailed(Func<Guid, string, DateTime, Task> handler)
    {
        hubConnection.On<Guid, string, DateTime>("AnalysisFailed", handler);
        logger.LogTrace("Subscribed to AnalysisFailed events");
    }

    private Task OnReconnecting(Exception? exception)
    {
        logger.LogWarning(exception, "SignalR connection lost, attempting to reconnect...");
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        logger.LogInformation("SignalR connection restored. ConnectionId: {ConnectionId}", connectionId);
        return Task.CompletedTask;
    }

    private Task OnClosed(Exception? exception)
    {
        if (exception != null)
        {
            logger.LogError(exception, "SignalR connection closed with error");
        }
        else
        {
            logger.LogInformation("SignalR connection closed");
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await hubConnection.DisposeAsync();
    }
}
