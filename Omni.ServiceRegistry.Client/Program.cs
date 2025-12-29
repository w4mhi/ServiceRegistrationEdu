using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Client;

// Configure host
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configure HTTP client
        services.AddHttpClient<ServiceRegistryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });
    })
    .Build();

// Get services
IServiceProvider serviceProvider = host.Services;
IHttpClientFactory httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
Microsoft.Extensions.Configuration.IConfiguration configuration = serviceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();

// API configuration
string apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? configuration["ServiceRegistry:ApiBaseUrl"]!;
Console.WriteLine($"=== Service Registry Client Simulator ===");
Console.WriteLine($"API Base URL: {apiBaseUrl}");
Console.WriteLine($"Starting 7 simulated services with demo cycle pattern...");
Console.WriteLine($"Pattern: Send 3-7 heartbeats → Miss until DEAD + 5-7 more → Recover → Repeat\n");

// Create cancellation token
using CancellationTokenSource cts = new();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n\nShutdown requested. Stopping all services...");
};

// Define service configurations (all using demo cycle)
List<(string Name, int Timeout, int MaxMissed, HealthScenario Scenario)> serviceConfigs = new()
{
    ("payment-service", 15, 3, HealthScenario.DemoCycle),
    ("inventory-service", 20, 4, HealthScenario.DemoCycle),
    ("notification-service", 25, 5, HealthScenario.DemoCycle),
    ("analytics-service", 30, 6, HealthScenario.DemoCycle),
    ("reporting-service", 18, 4, HealthScenario.DemoCycle),
    ("auth-service", 22, 5, HealthScenario.DemoCycle),
    ("logging-service", 20, 4, HealthScenario.DemoCycle)
};

// Create and start all simulated services
List<Task> serviceTasks = new();

foreach ((string name, int timeout, int maxMissed, HealthScenario scenario) in serviceConfigs)
{
    // Add random delay between service starts
    await Task.Delay(Random.Shared.Next(500, 2000));

    HttpClient httpClient = httpClientFactory.CreateClient();
    ILogger<ServiceRegistryClient> clientLogger = loggerFactory.CreateLogger<ServiceRegistryClient>();
    ILogger<SimulatedService> serviceLogger = loggerFactory.CreateLogger<SimulatedService>();

    ServiceRegistryClient client = new(httpClient, clientLogger, apiBaseUrl);
    SimulatedService service = new(client, serviceLogger, name, timeout, maxMissed, scenario);

    Task serviceTask = Task.Run(async () =>
    {
        try
        {
            await service.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
#pragma warning disable CA1031 // Catch all exceptions for top-level error handling
        catch (Exception ex)
        {
            Console.WriteLine($"Error in service {name}: {ex.Message}");
        }
#pragma warning restore CA1031
    }, cts.Token);

    serviceTasks.Add(serviceTask);
}

Console.WriteLine("\n✓ All 7 services started!");
Console.WriteLine("\nLegend:");
Console.WriteLine("  ✓ = Heartbeat sent successfully");
Console.WriteLine("  ✗ = Heartbeat failed to send");
Console.WriteLine("  ⊘ = Heartbeat intentionally skipped (simulating miss)");
Console.WriteLine("  💀 = Service entering death phase (will miss 15-20 heartbeats)");
Console.WriteLine("  🚑 = Service recovering and restarting cycle");
Console.WriteLine("\nPress Ctrl+C to stop all services...\n");

// Wait for all services to complete or cancellation
try
{
    await Task.WhenAll(serviceTasks);
}
catch (OperationCanceledException)
{
    Console.WriteLine("All services stopped.");
}

Console.WriteLine("\n=== Simulation Complete ===");
