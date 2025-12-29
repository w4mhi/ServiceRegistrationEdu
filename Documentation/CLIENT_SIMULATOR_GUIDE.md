# Service Registry Client Simulator - Complete Guide

## 🎯 Overview

The Service Registry Client Simulator is a testing tool that creates **5 simulated services** with different health behavior patterns to thoroughly test the Service Registry's state transition logic and monitoring capabilities.

## ✨ Features

- **5 Concurrent Services** - Each with unique behavior patterns
- **Realistic State Transitions** - Tests all health states: HEALTHY → UNHEALTHY → DEGRADED → DEAD → RECOVERED → HEALTHY
- **Automated Workflow** - Auto-registration, approval simulation, and heartbeat management
- **Visual Feedback** - Console logging with intuitive symbols
- **Configurable** - Easy to customize timing, scenarios, and service count

## 🔧 Architecture

### Components

```
Omni.ServiceRegistry.Client/
├── Models/
│   ├── RegistrationRequestDto.cs      - Registration payload
│   ├── RegistrationResponseDto.cs     - Registration response
│   └── HeartbeatRequestDto.cs         - Heartbeat payload
├── HealthScenario.cs                  - Scenario enum
├── ServiceRegistryClient.cs           - HTTP client wrapper
├── SimulatedService.cs                - Service lifecycle logic
├── Program.cs                         - Main entry point
└── appsettings.json                   - Configuration

Quick Start Script:
└── run-client-simulator.sh            - Automated launcher
```

### Workflow

```
1. Program.cs creates 5 SimulatedService instances
   ↓
2. Each SimulatedService:
   a. Registers via ServiceRegistryClient
   b. Waits for approval (simulated)
   c. Starts heartbeat loop based on HealthScenario
   ↓
3. ServiceRegistryClient sends HTTP requests to API:
   - POST /api/v1/register
   - GET /api/v1/status/registration/{id}
   - POST /api/v1/heartbeat/{serviceId}
   - POST /api/v1/admin/registrations/{id}/approve
```

## 🎭 Health Scenarios Explained

### 1. AlwaysHealthy (payment-service)
```
Config: 15s timeout, 3 max missed
Pattern: ✓✓✓✓✓✓✓✓✓✓ (100% success)
States:  HEALTHY → HEALTHY → HEALTHY
Purpose: Baseline for stable service
```

### 2. OccasionalMisses (inventory-service)
```
Config: 20s timeout, 4 max missed
Pattern: ✓✓✓✓⊘✓✓✓✓⊘ (80% success)
States:  HEALTHY → UNHEALTHY → HEALTHY
Purpose: Test recovery from single misses
```

### 3. FrequentMisses (notification-service)
```
Config: 25s timeout, 5 max missed
Pattern: ✓✓⊘✓✓⊘✓✓⊘ (67% success)
States:  HEALTHY → UNHEALTHY → DEGRADED → HEALTHY
Purpose: Test degradation threshold
```

### 4. DeadThenRecover (analytics-service)
```
Config: 30s timeout, 6 max missed

Cycle 1-5:   ✓✓✓✓✓           → HEALTHY
Cycle 6-10:  ✓⊘✓⊘✓           → UNHEALTHY
Cycle 11-15: ⊘⊘⊘⊘⊘           → DEGRADED
Cycle 16-20: ⊘⊘⊘⊘⊘           → DEAD
Cycle 21-25: ✓✓✓✓✓           → RECOVERED
Cycle 26+:   ✓✓✓✓✓✓✓✓✓✓     → HEALTHY

Purpose: Full state transition cycle test
```

### 5. Chaotic (reporting-service)
```
Config: 18s timeout, 4 max missed
Pattern: ✓⊘✓✓⊘✓⊘✓✓✓⊘⊘✓ (70% success, random)
States:  Unpredictable transitions
Timing:  Random variations (±1-3 seconds)
Purpose: Real-world unpredictable behavior
```

## 🚀 Quick Start

### Prerequisites

1. **.NET 9.0 SDK** installed
2. **Service Registry API** running
3. **PostgreSQL** (or InMemory) database configured

### Method 1: Using Quick Start Script (Recommended)

```bash
# Terminal 1: Start API
cd src/Omni.ServiceRegistry.Api
dotnet run

# Terminal 2: Run simulator (in repo root)
./run-client-simulator.sh
```

### Method 2: Manual Start

```bash
# Terminal 1: Start API
cd src/Omni.ServiceRegistry.Api
dotnet run

# Terminal 2: Build and run client
cd src/Omni.ServiceRegistry.Client
dotnet build
dotnet run
```

### Method 3: Custom API URL

```bash
# Use environment variable
API_BASE_URL=http://localhost:5159 ./run-client-simulator.sh

# Or set before running
export API_BASE_URL=http://localhost:5159
cd src/Omni.ServiceRegistry.Client
dotnet run
```

## 📊 Monitoring the Simulation

### Console Output

```
=== Service Registry Client Simulator ===
API Base URL: http://localhost:8080
Starting 5 simulated services with different health scenarios...

info: Starting simulated service 'payment-service' with scenario: AlwaysHealthy
info: Successfully registered service 'payment-service' with RegistrationId: abc123...
info: Service 'payment-service' approved with ServiceId: def456...
info: [payment-service] ✓ Heartbeat #1 sent (Scenario: AlwaysHealthy)

info: Starting simulated service 'analytics-service' with scenario: DeadThenRecover
info: [analytics-service] ✓ Heartbeat #1 sent
...
warn: [analytics-service] ⊘ Heartbeat #16 SKIPPED (simulating miss)
...

Legend:
  ✓ = Heartbeat sent successfully
  ✗ = Heartbeat failed to send
  ⊘ = Heartbeat intentionally skipped (simulating miss)
```

### Dashboard Monitoring

1. **Start Dashboard**:
```bash
cd src/Omni.ServiceRegistry.Dashboard
dotnet run
# Open: http://localhost:5160
```

2. **Watch Metrics**:
   - Total Services: Should show 5
   - Health Status Counts: Real-time transitions
   - Service Detail: Individual heartbeat history

3. **Expected Dashboard States**:
   - **payment-service**: Always green (HEALTHY)
   - **inventory-service**: Occasional yellow (UNHEALTHY)
   - **notification-service**: Yellow/orange transitions
   - **analytics-service**: Full color cycle (green → yellow → orange → red → blue → green)
   - **reporting-service**: Random color changes

### Database Queries

```sql
-- View all simulated services
SELECT 
    "ServiceName", 
    "HealthStatus", 
    "MissedHeartbeatCounter", 
    "HeartbeatCount", 
    "LastHeartbeatTimestamp"
FROM "Services"
WHERE "ServiceName" IN (
    'payment-service',
    'inventory-service',
    'notification-service',
    'analytics-service',
    'reporting-service'
)
ORDER BY "ServiceName";

-- Track state transitions
SELECT 
    s."ServiceName",
    h."ChangeType",
    h."OldValue",
    h."NewValue",
    h."ChangedAt"
FROM "ServiceChangeHistory" h
JOIN "Services" s ON h."ServiceId" = s."ServiceId"
WHERE h."ChangeType" = 'HealthStatus'
ORDER BY h."ChangedAt" DESC
LIMIT 50;

-- Heartbeat statistics
SELECT 
    "ServiceName",
    "HeartbeatCount",
    COUNT(*) FILTER (WHERE "HealthStatus" = 0) as "HealthyCount",
    COUNT(*) FILTER (WHERE "HealthStatus" = 1) as "UnhealthyCount",
    COUNT(*) FILTER (WHERE "HealthStatus" = 2) as "DegradedCount"
FROM "Services"
GROUP BY "ServiceName", "HeartbeatCount";
```

## 🧪 Testing Scenarios

### Test 1: Baseline Health
**Service**: payment-service  
**Duration**: 5 minutes  
**Expected**:
- ✅ All heartbeats sent successfully
- ✅ MissedHeartbeatCounter always 0
- ✅ HealthStatus remains HEALTHY
- ✅ Dashboard shows solid green

**Verification**:
```sql
SELECT "HeartbeatCount", "MissedHeartbeatCounter", "HealthStatus"
FROM "Services"
WHERE "ServiceName" = 'payment-service';
-- Should show: HeartbeatCount > 0, MissedHeartbeatCounter = 0, HealthStatus = 0
```

### Test 2: Transient Failure Recovery
**Service**: inventory-service  
**Duration**: 10 minutes  
**Expected**:
- ✅ Misses every 5th heartbeat
- ✅ Transitions: HEALTHY → UNHEALTHY → HEALTHY
- ✅ Never reaches DEGRADED
- ✅ MissedHeartbeatCounter resets after recovery

**Verification**:
```sql
SELECT "ChangeType", "OldValue", "NewValue", "ChangedAt"
FROM "ServiceChangeHistory" h
JOIN "Services" s ON h."ServiceId" = s."ServiceId"
WHERE s."ServiceName" = 'inventory-service'
  AND "ChangeType" = 'HealthStatus'
ORDER BY "ChangedAt";
-- Should show alternating Healthy/Unhealthy transitions
```

### Test 3: Degradation Threshold
**Service**: notification-service  
**Duration**: 15 minutes  
**Expected**:
- ✅ Reaches DEGRADED state
- ✅ Transitions: HEALTHY → UNHEALTHY → DEGRADED → HEALTHY
- ✅ Never reaches DEAD
- ✅ Recovers from DEGRADED

**Verification**:
- Dashboard shows orange color during degradation
- MissedHeartbeatCounter reaches 50% of max

### Test 4: Death and Resurrection
**Service**: analytics-service  
**Duration**: 30 minutes (full cycle)  
**Expected Timeline**:
- Minutes 0-5: HEALTHY (green)
- Minutes 5-10: UNHEALTHY (yellow)
- Minutes 10-15: DEGRADED (orange)
- Minutes 15-20: DEAD (red)
- Minutes 20-25: RECOVERED (blue)
- Minutes 25+: HEALTHY (green)

**Verification**:
```sql
-- Should show all 6 states
SELECT DISTINCT "NewValue" as "State"
FROM "ServiceChangeHistory" h
JOIN "Services" s ON h."ServiceId" = s."ServiceId"
WHERE s."ServiceName" = 'analytics-service'
  AND "ChangeType" = 'HealthStatus'
ORDER BY "State";
-- Expected: 0=HEALTHY, 1=UNHEALTHY, 2=DEGRADED, 3=DEAD, 4=RECOVERED
```

### Test 5: Chaos Engineering
**Service**: reporting-service  
**Duration**: 20 minutes  
**Expected**:
- ✅ Unpredictable state changes
- ✅ Random timing variations
- ✅ Multiple state transitions
- ✅ Eventually stabilizes or cycles

**Verification**:
- Count transitions > other services
- Timing between heartbeats varies
- Shows real-world behavior patterns

## ⚙️ Configuration

### Service Count

Edit `Program.cs`:
```csharp
List<(string Name, int Timeout, int MaxMissed, HealthScenario Scenario)> serviceConfigs = new()
{
    ("service-1", 15, 3, HealthScenario.AlwaysHealthy),
    ("service-2", 20, 4, HealthScenario.OccasionalMisses),
    // Add more or remove...
};
```

### Custom Scenario

1. Add to `HealthScenario.cs`:
```csharp
public enum HealthScenario
{
    // ... existing
    MyCustomScenario
}
```

2. Implement in `SimulatedService.cs`:
```csharp
private bool ShouldSendHeartbeatInThisCycle()
{
    return scenario switch
    {
        // ... existing
        HealthScenario.MyCustomScenario => /* your logic */,
        _ => true
    };
}
```

### Timing Adjustments

Edit `SimulatedService.cs`:
```csharp
private int CalculateWaitTime()
{
    // Current: heartbeatTimeout - 2 seconds
    int baseWait = (heartbeatTimeout * 1000) - 2000;
    
    // Customize:
    int buffer = 3000; // 3 second buffer
    int baseWait = (heartbeatTimeout * 1000) - buffer;
    
    return baseWait;
}
```

### API URL

Three methods:
1. Environment variable: `export API_BASE_URL=http://example.com`
2. Script parameter: `API_BASE_URL=http://example.com ./run-client-simulator.sh`
3. appsettings.json: `"ServiceRegistry": { "ApiBaseUrl": "..." }`

## 🐛 Troubleshooting

### Issue: "API is not running"

**Symptoms**: Script exits with connection error

**Solutions**:
```bash
# 1. Check API is running
curl http://localhost:8080/health

# 2. Verify correct port
netstat -an | grep 8080

# 3. Check API logs
cd src/Omni.ServiceRegistry.Api
dotnet run
```

### Issue: All services stuck in PENDING

**Cause**: Auto-approval not working

**Solutions**:
```bash
# Manual approval via API
curl -X POST http://localhost:8080/api/v1/admin/registrations/{id}/approve \
  -H "Content-Type: application/json" \
  -d '{"approvedBy": "admin", "comments": "Approved"}'

# Or via Dashboard
# Open http://localhost:5160/approvals
# Click "Approve" for each service
```

### Issue: No state transitions visible

**Cause**: HeartbeatMonitorService not running

**Solutions**:
```bash
# 1. Check API logs for background service
# Look for: "HeartbeatMonitorService starting"

# 2. Verify configuration
grep -A 5 "HeartbeatMonitoring" src/Omni.ServiceRegistry.Api/appsettings.json

# 3. Ensure CheckIntervalSeconds is set
# Should be: "CheckIntervalSeconds": 5
```

### Issue: Services immediately go DEAD

**Cause**: Heartbeat timeout too short

**Solutions**:
- Increase timeout in serviceConfigs
- Reduce HeartbeatMonitorService check interval
- Check network latency between client and API

### Issue: Build errors

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build

# Check .NET version
dotnet --version  # Should be 9.0+
```

## 📈 Performance

### Resource Usage
- **Memory**: ~10-15MB per service instance
- **CPU**: <1% per service (idle between heartbeats)
- **Network**: ~5-10 requests/minute per service
- **Total Load**: 25-50 API requests/minute (5 services)

### Scaling
- Safe to run up to 50 concurrent services on standard hardware
- Each service is independent and non-blocking
- Async/await throughout for efficient I/O

### Recommendations
- Use for load testing: Increase service count
- Use for stress testing: Add more Chaotic scenarios
- Use for longevity testing: Run for 24+ hours

## 🔍 Advanced Usage

### Custom Approval Flow

Implement actual approval polling:
```csharp
private async Task<bool> WaitForApprovalAsync(CancellationToken cancellationToken)
{
    for (int i = 0; i < 30; i++) // 30 attempts
    {
        RegistrationResponseDto? status = 
            await client.GetRegistrationStatusAsync(registrationId);
        
        if (status?.Status == "approved")
        {
            serviceId = status.ServiceId;
            return true;
        }
        
        await Task.Delay(2000, cancellationToken); // Check every 2s
    }
    
    return false; // Timeout after 60s
}
```

### Metrics Collection

Add Prometheus metrics:
```csharp
// In SimulatedService
private Counter heartbeatsSent = Metrics.CreateCounter(
    "simulated_heartbeats_sent", 
    "Total heartbeats sent");

private void RecordMetrics(bool success)
{
    heartbeatsSent.Inc();
    if (!success) heartbeatsFailed.Inc();
}
```

### Graceful Shutdown

Handle cleanup on exit:
```csharp
// In SimulatedService
public async Task StopAsync()
{
    isRunning = false;
    
    // Send final heartbeat
    if (serviceId.HasValue)
    {
        await client.SendHeartbeatAsync(serviceId.Value);
    }
    
    logger.LogInformation("Service {ServiceName} stopped gracefully", serviceName);
}
```

## 📚 Integration with CI/CD

### Automated Testing

```yaml
# .github/workflows/integration-test.yml
- name: Start API
  run: |
    cd src/Omni.ServiceRegistry.Api
    dotnet run &
    sleep 10

- name: Run Client Simulator
  run: |
    timeout 120 ./run-client-simulator.sh || true

- name: Verify Results
  run: |
    # Query database to verify states
    psql -c "SELECT COUNT(*) FROM Services WHERE HealthStatus = 0"
```

### Docker Compose

```yaml
# docker-compose.test.yml
services:
  client-simulator:
    build:
      context: .
      dockerfile: src/Omni.ServiceRegistry.Client/Dockerfile
    environment:
      API_BASE_URL: http://api:8080
    depends_on:
      - api
```

## 🎓 Educational Use

This simulator is excellent for:
- **Learning state machines**: Observe real-time state transitions
- **Testing distributed systems**: Understand heartbeat patterns
- **Monitoring demonstrations**: Show dashboard capabilities
- **Failure injection**: Simulate various failure modes
- **Performance testing**: Generate realistic load

## 📝 Future Enhancements

- [ ] Web UI for scenario configuration
- [ ] Prometheus metrics exporter
- [ ] Configurable scenario definitions via JSON
- [ ] Service deletion simulation
- [ ] Support for service updates
- [ ] Dependency graph simulation
- [ ] Historical replay from logs

## 🤝 Contributing

To add new scenarios:
1. Update `HealthScenario` enum
2. Implement logic in `SimulatedService.ShouldSendHeartbeatInThisCycle()`
3. Add to default configs in `Program.cs`
4. Document behavior in README
5. Add test case verification

## 📄 License

Part of the Omni Service Registry project.
