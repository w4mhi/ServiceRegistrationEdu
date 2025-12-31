using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Omni.ServiceRegistry.Client;
using Omni.ServiceRegistry.Client.Models;
using Omni.ServiceRegistry.Client.Services;
using Omni.ServiceRegistry.Models;

// Build configuration manually first
ConfigurationBuilder configBuilder = new();
configBuilder.SetBasePath(Directory.GetCurrentDirectory());
configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
configBuilder.AddEnvironmentVariables();
IConfigurationRoot configuration = configBuilder.Build();

// Configure host
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configure HTTP client
        services.AddHttpClient<ServiceRegistryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register OpenApiParser
        services.AddSingleton<OpenApiParser>();

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
OpenApiParser openApiParser = serviceProvider.GetRequiredService<OpenApiParser>();

// API configuration (using manually built configuration)
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

// Load service configurations dynamically from appsettings.json
IConfigurationSection servicesConfig = configuration.GetSection("Services");

List<ServiceConfiguration> serviceConfigs = new();
foreach (IConfigurationSection serviceSection in servicesConfig.GetChildren())
{
    ServiceConfiguration? config = serviceSection.Get<ServiceConfiguration>();
    if (config != null)
    {
        serviceConfigs.Add(config);
    }
    else
    {
        Console.WriteLine($"Warning: Failed to parse service configuration for section '{serviceSection.Key}'");
    }
}

if (serviceConfigs.Count == 0)
{
    Console.WriteLine("Error: No service configurations found in appsettings.json under 'Services' section.");
    return;
}

Console.WriteLine($"Starting {serviceConfigs.Count} simulated services...\n");

// Create and start all simulated services
List<Task> serviceTasks = new();

foreach (ServiceConfiguration config in serviceConfigs)
{
    // Add random delay between service starts
    await Task.Delay(Random.Shared.Next(500, 2000));

    // Parse OpenAPI spec if provided
    List<ApiEndpoint> apiEndpoints = new();
    if (!string.IsNullOrEmpty(config.OpenApiSpecPath))
    {
        string specPath = Path.Combine(AppContext.BaseDirectory, config.OpenApiSpecPath);
        apiEndpoints = await openApiParser.ParseSpecFileAsync(specPath);
        Console.WriteLine($"📋 {config.Name}: {apiEndpoints.Count} API endpoints loaded from {config.OpenApiSpecPath}");
    }
    else
    {
        Console.WriteLine($"📋 {config.Name}: No OpenAPI spec configured");
    }

    HttpClient httpClient = httpClientFactory.CreateClient();
    ILogger<ServiceRegistryClient> clientLogger = loggerFactory.CreateLogger<ServiceRegistryClient>();
    ILogger<SimulatedService> serviceLogger = loggerFactory.CreateLogger<SimulatedService>();

    ServiceRegistryClient client = new(httpClient, clientLogger, apiBaseUrl);
    
    HealthScenario scenario = Enum.Parse<HealthScenario>(config.Scenario);
    
    SimulatedService service = new(
        client,
        serviceLogger,
        config.Name,
        config.Description,
        config.ContactEmail,
        config.Endpoints,
        apiEndpoints,
        config.HeartbeatTimeout,
        config.MaxMissedHeartbeats,
        scenario);

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
            Console.WriteLine($"Error in service {config.Name}: {ex.Message}");
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
