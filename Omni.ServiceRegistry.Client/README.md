# Service Registry Client Simulator

A testing client that simulates multiple services with different health behavior patterns to test the Service Registry's state transition logic.

## Features

- **5 Simulated Services** with different health scenarios
- **Realistic State Transitions**: HEALTHY → UNHEALTHY → DEGRADED → DEAD → RECOVERED → HEALTHY
- **Configurable Parameters**: Heartbeat timeout and max missed heartbeats
- **Visual Feedback**: Console logging with symbols (✓ ✗ ⊘)
- **Auto-registration and approval simulation**

## Health Scenarios

### 1. AlwaysHealthy (payment-service)
- Never misses heartbeats
- Remains in HEALTHY state
- **Parameters**: 15s timeout, 3 max missed

### 2. OccasionalMisses (inventory-service)
- Misses every 5th heartbeat
- Transitions: HEALTHY → UNHEALTHY → HEALTHY
- **Parameters**: 20s timeout, 4 max missed

### 3. FrequentMisses (notification-service)
- Misses every 3rd heartbeat
- Transitions: HEALTHY → UNHEALTHY → DEGRADED → HEALTHY
- **Parameters**: 25s timeout, 5 max missed

### 4. DeadThenRecover (analytics-service)
- Full cycle: Goes dead then recovers
- **Phases**:
  - Cycles 1-5: Healthy (all heartbeats sent)
  - Cycles 6-10: Start missing → UNHEALTHY
  - Cycles 11-15: Miss more → DEGRADED
  - Cycles 16-20: Miss all → DEAD
  - Cycles 21-25: Recover → RECOVERED
  - Cycles 26+: Stable → HEALTHY
- **Parameters**: 30s timeout, 6 max missed

### 5. Chaotic (reporting-service)
- Random behavior (70% success rate)
- Unpredictable state transitions
- Includes random timing variations (±1-3 seconds)
- **Parameters**: 18s timeout, 4 max missed

## Prerequisites

- .NET 9.0 SDK
- Service Registry API running (default: http://localhost:8080)

## Quick Start

### 1. Start the Service Registry API

```bash
# Terminal 1: Start API
cd /path/to/playground/src/Omni.ServiceRegistry.Api
dotnet run
```

### 2. Run the Client Simulator

```bash
# Terminal 2: Start simulator
cd /path/to/playground/src/Omni.ServiceRegistry.Client
dotnet run
```

### 3. Monitor the Dashboard

```bash
# Terminal 3: Start dashboard
cd /path/to/playground/src/Omni.ServiceRegistry.Dashboard
dotnet run

# Open browser: http://localhost:5160
```

## Configuration

### Environment Variable

```bash
# Change API URL
export API_BASE_URL=http://localhost:5159
dotnet run
```

### appsettings.json

```json
{
  "ServiceRegistry": {
    "ApiBaseUrl": "http://localhost:8080"
  }
}
```

## Output Example

```
=== Service Registry Client Simulator ===
API Base URL: http://localhost:8080
Starting 5 simulated services with different health scenarios...

info: SimulatedService[0]
      Starting simulated service 'payment-service' with scenario: AlwaysHealthy, timeout: 15s, max missed: 3
info: ServiceRegistryClient[0]
      Successfully registered service 'payment-service' with RegistrationId: a1b2c3d4-...
info: SimulatedService[0]
      Service 'payment-service' approved with ServiceId: e5f6g7h8-...
info: SimulatedService[0]
      [payment-service] ✓ Heartbeat #1 sent (Scenario: AlwaysHealthy)

info: SimulatedService[0]
      Starting simulated service 'analytics-service' with scenario: DeadThenRecover, timeout: 30s, max missed: 6
info: SimulatedService[0]
      [analytics-service] ✓ Heartbeat #1 sent (Scenario: DeadThenRecover)
...
warn: SimulatedService[0]
      [analytics-service] ⊘ Heartbeat #16 SKIPPED (simulating miss)
...

✓ All 5 services started!

Legend:
  ✓ = Heartbeat sent successfully
  ✗ = Heartbeat failed to send
  ⊘ = Heartbeat intentionally skipped (simulating miss)

Press Ctrl+C to stop all services...
```

## Testing Scenarios

### Test 1: Always Healthy
**Service**: payment-service  
**Expected**: Remains HEALTHY throughout  
**Verify**: Dashboard shows green status continuously

### Test 2: Occasional Misses
**Service**: inventory-service  
**Expected**: Briefly goes UNHEALTHY, then recovers  
**Verify**: Dashboard shows yellow status intermittently

### Test 3: Degradation
**Service**: notification-service  
**Expected**: HEALTHY → UNHEALTHY → DEGRADED → recovers  
**Verify**: Dashboard shows status progression

### Test 4: Death and Recovery
**Service**: analytics-service  
**Expected**: Full cycle ending in recovery  
**Verify**:
- Cycles 1-5: Green (HEALTHY)
- Cycles 6-10: Yellow (UNHEALTHY)
- Cycles 11-15: Orange (DEGRADED)
- Cycles 16-20: Red (DEAD)
- Cycles 21-25: Blue (RECOVERED)
- Cycles 26+: Green (HEALTHY)

### Test 5: Chaos
**Service**: reporting-service  
**Expected**: Unpredictable behavior  
**Verify**: Dashboard shows various states randomly

## API Endpoints Used

- `POST /api/v1/register` - Register service
- `GET /api/v1/status/registration/{id}` - Check registration status
- `POST /api/v1/heartbeat/{serviceId}` - Send heartbeat
- `POST /api/v1/admin/registrations/{id}/approve` - Auto-approve (testing)

## Implementation Details

### ServiceRegistryClient
- HTTP client wrapper for API communication
- Methods: Register, GetStatus, SendHeartbeat, Approve
- Error handling and retry logic

### SimulatedService
- Lifecycle management: Register → Approve → Heartbeat loop
- Scenario-based behavior patterns
- Configurable timing and miss patterns

### HealthScenario Enum
- AlwaysHealthy
- OccasionalMisses
- FrequentMisses
- DeadThenRecover
- Chaotic

## Monitoring

### Dashboard Metrics
Monitor the following in the Service Registry Dashboard:

1. **Total Services**: Should show 5 services
2. **Health Status Counts**: Watch transitions in real-time
3. **Service Detail Page**: View individual service heartbeat history
4. **Last Heartbeat**: Check timestamps and missed counters

### API Logs
The API logs will show:
- Service registrations
- Approval events
- Heartbeat receipts
- Health status transitions
- Background monitor activities

### Database Queries
```sql
-- Check service health statuses
SELECT "ServiceName", "HealthStatus", "MissedHeartbeatCounter", "HeartbeatCount", "LastHeartbeatTimestamp"
FROM "Services"
ORDER BY "ServiceName";

-- View state transitions
SELECT "ServiceId", "ChangeType", "OldValue", "NewValue", "ChangedAt"
FROM "ServiceChangeHistory"
WHERE "ChangeType" = 'HealthStatus'
ORDER BY "ChangedAt" DESC
LIMIT 20;
```

## Troubleshooting

### Services Not Registering
```bash
# Check API is running
curl http://localhost:8080/health

# Check logs
dotnet run --verbosity detailed
```

### Heartbeats Not Processed
- Verify HeartbeatMonitorService is running
- Check API logs for errors
- Ensure database connection is working

### All Services DEAD
- Background monitor may not be running
- Check API HeartbeatMonitoring configuration
- Increase heartbeat timeout for testing

## Customization

### Add New Scenario
```csharp
// In HealthScenario enum
public enum HealthScenario
{
    // ... existing
    MyCustomScenario
}

// In SimulatedService.ShouldSendHeartbeatInThisCycle()
HealthScenario.MyCustomScenario => /* your logic */,
```

### Adjust Service Count
```csharp
// In Program.cs
List<(string, int, int, HealthScenario)> serviceConfigs = new()
{
    // Add more or remove entries
    ("my-service", 20, 5, HealthScenario.Chaotic)
};
```

### Change Timing
```csharp
// In SimulatedService.CalculateWaitTime()
int baseWait = (heartbeatTimeout * 1000) - 2000; // Adjust buffer
```

## Development

### Build
```bash
dotnet build
```

### Run with Custom API
```bash
API_BASE_URL=https://api.example.com dotnet run
```

### Debug
```bash
dotnet run --verbosity detailed
```

## Architecture

```
Program.cs
    ├── Creates 5 SimulatedService instances
    │   └── Each with different HealthScenario
    │
    └── Each SimulatedService
        ├── Uses ServiceRegistryClient
        │   └── HttpClient → Service Registry API
        │
        └── Runs lifecycle:
            1. Register
            2. Wait for approval
            3. Heartbeat loop (scenario-based)
```

## Performance

- **5 concurrent services**
- **~3-5 heartbeats per minute per service**
- **Total load**: 15-25 requests/minute
- **Minimal resource usage**: <10MB memory

## Future Enhancements

- [ ] Read configuration from appsettings.json
- [ ] Implement actual approval polling
- [ ] Add metrics collection
- [ ] Support custom scenario definitions via JSON
- [ ] Add service deletion simulation
- [ ] Implement graceful shutdown with cleanup
- [ ] Add Prometheus metrics exporter
- [ ] Support multiple API endpoints (load balancing)

## License

Part of the Service Registry project.
