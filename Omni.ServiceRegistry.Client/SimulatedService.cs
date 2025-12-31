using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Omni.ServiceRegistry.Client.Models;

namespace Omni.ServiceRegistry.Client;

/// <summary>
/// Simulates a service with various health behavior patterns
/// </summary>
public class SimulatedService
{
    private readonly ServiceRegistryClient client;
    private readonly ILogger<SimulatedService> logger;
    private readonly string serviceName;
    private readonly int heartbeatTimeout;
    private readonly int maxMissedHeartbeats;
    private readonly HealthScenario scenario;
    private readonly Random random;

    private Guid? serviceId;
    private Guid? registrationId;
    private bool isRunning;
    private int cycleCount;
    private int healthyHeartbeatsToSend;
    private int missedHeartbeatsToSend;
    private int healthyHeartbeatsSent;
    private int missedHeartbeatsSent;
    private bool inRecoveryPhase;
    private int recoveryHeartbeatsSent;

    public SimulatedService(
        ServiceRegistryClient client,
        ILogger<SimulatedService> logger,
        string serviceName,
        int heartbeatTimeout,
        int maxMissedHeartbeats,
        HealthScenario scenario)
    {
        this.client = client;
        this.logger = logger;
        this.serviceName = serviceName;
        this.heartbeatTimeout = heartbeatTimeout;
        this.maxMissedHeartbeats = maxMissedHeartbeats;
        this.scenario = scenario;
        this.random = new Random();
        this.cycleCount = 0;
        this.healthyHeartbeatsToSend = random.Next(3, 8); // 3-7 heartbeats
        this.missedHeartbeatsToSend = maxMissedHeartbeats + random.Next(5, 8); // DEAD + 5-7 more missed
        this.healthyHeartbeatsSent = 0;
        this.missedHeartbeatsSent = 0;
        this.inRecoveryPhase = false;
        this.recoveryHeartbeatsSent = 0;
    }

    /// <summary>
    /// Start the simulated service lifecycle
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting simulated service '{ServiceName}' with scenario: {Scenario}, timeout: {Timeout}s, max missed: {MaxMissed}",
            serviceName, scenario, heartbeatTimeout, maxMissedHeartbeats);

        // Step 1: Register
        if (!await RegisterAsync())
        {
            logger.LogError("Failed to register service '{ServiceName}'. Exiting.", serviceName);
            return;
        }

        // Step 2: Wait for approval (service must be approved before heartbeats can be sent)
        if (!serviceId.HasValue)
        {
            bool approved = await WaitForApprovalAsync(cancellationToken);
            if (!approved)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogError("Service '{ServiceName}' was not approved. Exiting.", serviceName);
                }
                return;
            }
            
            // Mark as approved and ready for scenario simulation
            logger.LogInformation("✓ Service '{ServiceName}' approved with ServiceId: {ServiceId}. Starting heartbeat simulation...", 
                serviceName, serviceId);
        }

        // Step 3: Start heartbeat loop with scenario-based simulation
        isRunning = true;
        await HeartbeatLoopAsync(cancellationToken);

        logger.LogInformation("Simulated service '{ServiceName}' stopped", serviceName);
    }

    private async Task<bool> RegisterAsync()
    {
        RegistrationRequestDto request = new()
        {
            ServiceName = serviceName,
            Description = $"Simulated service testing {scenario} scenario",
            ContactEmail = $"{serviceName}@simulation.local",
            Endpoints = new List<string> { $"https://api.simulation.local/{serviceName}" },
            HeartbeatTimeout = heartbeatTimeout,
            MaxMissedHeartbeats = maxMissedHeartbeats
        };

        RegistrationResponseDto? registrationResponse = await client.RegisterServiceAsync(request);
        
        if (registrationResponse == null)
        {
            logger.LogError("Failed to register service '{ServiceName}'. Exiting.", serviceName);
            return false;
        }
        
        registrationId = registrationResponse.RegistrationId;
        
        // Check if service is already approved (idempotent registration or auto-approval)
        if (registrationResponse.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase) && registrationResponse.ServiceId.HasValue)
        {
            serviceId = registrationResponse.ServiceId.Value;
            logger.LogInformation(
                "Service '{ServiceName}' already approved with ServiceId: {ServiceId}. Skipping approval wait.",
                serviceName, serviceId);
            return true; // Already approved, skip wait
        }
        
        logger.LogInformation("Successfully registered service '{ServiceName}' with RegistrationId: {RegistrationId}",
            serviceName, registrationId);
        return true;
    }

    private async Task<bool> WaitForApprovalAsync(CancellationToken cancellationToken)
    {
        if (!registrationId.HasValue)
        {
            logger.LogError("Cannot wait for approval - no RegistrationId available");
            return false;
        }

        logger.LogInformation("Waiting for approval of '{ServiceName}'... (RegistrationId: {RegistrationId})", serviceName, registrationId);

        try
        {
            // Poll for approval status indefinitely (no timeout - for demo purposes)
            while (true)
            {
                RegistrationResponseDto? status = await client.GetRegistrationStatusAsync(registrationId.Value);
                
                if (status != null && status.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase) && status.ServiceId.HasValue)
                {
                    serviceId = status.ServiceId.Value;
                    logger.LogInformation("Service '{ServiceName}' approved with ServiceId: {ServiceId}", serviceName, serviceId);
                    return true;
                }
                
                if (status != null && status.Status == "denied")
                {
                    logger.LogWarning("Service '{ServiceName}' was denied", serviceName);
                    return false;
                }
                
                await Task.Delay(1000, cancellationToken); // Check every second
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested, exit gracefully without error
            logger.LogInformation("Service '{ServiceName}' approval wait cancelled (shutdown)", serviceName);
            return false;
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        if (!serviceId.HasValue)
        {
            logger.LogError("Cannot start heartbeat loop - no ServiceId assigned");
            return;
        }

        logger.LogInformation("[{ServiceName}] Starting heartbeat loop with scenario: {Scenario}", serviceName, scenario);

        while (isRunning && !cancellationToken.IsCancellationRequested)
        {
            cycleCount++;
            bool shouldSendHeartbeat = ShouldSendHeartbeatInThisCycle();

            if (shouldSendHeartbeat)
            {
                bool success = await client.SendHeartbeatAsync(serviceId.Value);
                if (success)
                {
                    logger.LogInformation(
                        "[{ServiceName}] ✓ Heartbeat #{Cycle} sent (Scenario: {Scenario})",
                        serviceName, cycleCount, scenario);
                }
                else
                {
                    logger.LogWarning(
                        "[{ServiceName}] ✗ Heartbeat #{Cycle} FAILED to send",
                        serviceName, cycleCount);
                }
            }
            else
            {
                logger.LogWarning(
                    "[{ServiceName}] ⊘ Heartbeat #{Cycle} SKIPPED (simulating miss)",
                    serviceName, cycleCount);
            }

            // Wait for next heartbeat interval
            int waitTime = CalculateWaitTime();
            await Task.Delay(waitTime, cancellationToken);
        }
    }

    /// <summary>
    /// Determines if heartbeat should be sent based on the scenario
    /// </summary>
    private bool ShouldSendHeartbeatInThisCycle()
    {
        // Service is approved - run scenario-based simulation
        
        // Phase 0: Recovery phase (need maxMissedHeartbeats consecutive heartbeats to fully recover)
        if (inRecoveryPhase)
        {
            recoveryHeartbeatsSent++;
            
            if (recoveryHeartbeatsSent >= maxMissedHeartbeats)
            {
                // Recovery complete, start new cycle
                logger.LogInformation(
                    "[{ServiceName}] ✓ RECOVERY COMPLETE after {Count} consecutive heartbeats",
                    serviceName, recoveryHeartbeatsSent);
                
                inRecoveryPhase = false;
                recoveryHeartbeatsSent = 0;
                healthyHeartbeatsToSend = random.Next(3, 8);
                missedHeartbeatsToSend = random.Next(15, 21);
                healthyHeartbeatsSent = 0;
                missedHeartbeatsSent = 0;
            }
            
            return true; // Always send during recovery
        }
        
        // Phase 1: Send healthy heartbeats (3-7 times)
        if (healthyHeartbeatsSent < healthyHeartbeatsToSend)
        {
            healthyHeartbeatsSent++;
            return true;
        }
        
        // Phase 2: Miss heartbeats (15-20 times) - service will die
        if (missedHeartbeatsSent < missedHeartbeatsToSend)
        {
            missedHeartbeatsSent++;
            
            if (missedHeartbeatsSent == 1)
            {
                logger.LogWarning(
                    "[{ServiceName}] 💀 Starting DEATH phase - will miss {Count} heartbeats",
                    serviceName, missedHeartbeatsToSend);
            }
            
            return false;
        }
        
        // Phase 3: Start recovery
        logger.LogInformation(
            "[{ServiceName}] 🚑 Starting RECOVERY phase - will send {Count} consecutive heartbeats",
            serviceName, maxMissedHeartbeats);
        
        inRecoveryPhase = true;
        recoveryHeartbeatsSent = 1; // Count this first one
        
        return true; // Send first recovery heartbeat
    }

    /// <summary>
    /// Calculates wait time until next heartbeat attempt
    /// </summary>
    private int CalculateWaitTime()
    {
        // During recovery phase, send heartbeats rapidly (every 2 seconds)
        // to ensure consecutive heartbeats arrive before the monitor increments the counter
        if (inRecoveryPhase)
        {
            return 2000; // 2 seconds during recovery
        }
        
        // Normal phase: wait until just before timeout
        int baseWait = (heartbeatTimeout * 1000) - 2000; // 2 seconds before timeout
        return Math.Max(baseWait, 2000); // Minimum 2 seconds
    }

    public void Stop()
    {
        isRunning = false;
        logger.LogInformation("Stopping simulated service '{ServiceName}'", serviceName);
    }
}
