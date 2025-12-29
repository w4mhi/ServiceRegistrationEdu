using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Dashboard.Components;
using Omni.ServiceRegistry.Dashboard.Hubs;
using Omni.ServiceRegistry.Dashboard.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure logging to file
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFile("/tmp/dashboard-{Date}.log", minimumLevel: LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Configure HTTP client to communicate with Service Registry API
string apiBaseUrl = builder.Configuration["ServiceRegistry:ApiBaseUrl"]!;
builder.Services.AddHttpClient<ServiceRegistryApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Configure SignalR hub client for receiving real-time updates from API
string apiHubUrl = $"{apiBaseUrl}/hubs/servicemonitor";
builder.Services.AddSingleton(sp =>
{
    ILogger<SignalRHubClient> logger = sp.GetRequiredService<ILogger<SignalRHubClient>>();
    return new SignalRHubClient(apiHubUrl, logger);
});

// Configure health checks
builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Health check endpoint
app.MapHealthChecks("/health");

// SignalR hub endpoint
app.MapHub<ServiceMonitoringHub>("/hubs/monitoring");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
