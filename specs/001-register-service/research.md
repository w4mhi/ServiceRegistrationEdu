# Research & Decisions: Service Registration

**Date**: November 9, 2025  
**Feature**: Service Registration with Heartbeat Monitoring  
**Purpose**: Resolve technical unknowns and establish implementation patterns

## 1. Storage Backend Abstraction & Repository Pattern

### Decision
Use **pluggable storage backend** with two-layer abstraction: `IStorageProvider` for low-level operations and `IServiceRepository` for domain-specific operations.

### Rationale
- Support multiple storage backends: PostgreSQL, Redis, SQL Server, File (testing)
- Developer works with domain models, not SQL queries or database-specific APIs
- Single backend active at runtime (configured via `appsettings.json`)
- Enables testing with in-memory or file-based implementations
- Avoids coupling business logic to specific database technology

### Implementation Pattern
```csharp
// Low-level storage abstraction
public interface IStorageProvider
{
    Task<T?> ReadAsync<T>(string key) where T : class;
    Task WriteAsync<T>(string key, T entity) where T : class;
    Task<bool> DeleteAsync(string key);
    Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate) where T : class;
}

// Domain-specific repository
public interface IServiceRepository
{
    Task<Service?> GetByIdAsync(Guid serviceId);
    Task<List<Service>> GetByHealthStatusAsync(HealthStatus status);
    Task<List<Service>> GetByOwnerAsync(string ownerEmail);
    Task<Service> AddAsync(Service service);
    Task UpdateAsync(Service service);
    Task<bool> ExistsByNormalizedNameAsync(string normalizedName);
}

// Configuration in appsettings.json
{
  "DatabaseProvider": "Postgres", // or "Redis", "SqlServer", "File"
  "ConnectionStrings": {
    "ServiceRegistry": "Host=localhost;Database=serviceregistry;..."
  }
}

// DI registration
services.AddScoped<IStorageProvider>(sp =>
{
    string provider = configuration["DatabaseProvider"];
    return provider switch
    {
        "Postgres" => new PostgresStorageProvider(connectionString),
        "Redis" => new RedisStorageProvider(connectionString),
        "File" => new FileStorageProvider(dataDirectory),
        _ => throw new InvalidOperationException($"Unknown provider: {provider}")
    };
});
services.AddScoped<IServiceRepository, ServiceRepository>();
```

### Alternatives Considered
- **Direct EF Core usage**: Rejected; couples code to SQL databases only
- **Single IRepository interface**: Too broad; split into IStorageProvider (generic) + IServiceRepository (domain-specific)
- **Multiple databases simultaneously**: Rejected; operator chooses one backend per deployment

### Storage Provider Implementations
- **PostgresStorageProvider**: Uses EF Core with DbContext, supports transactions, migrations
- **RedisStorageProvider**: Uses StackExchange.Redis with RedisJSON module for document storage
- **FileStorageProvider**: JSON files in directory structure (testing only)

### References
- [Repository Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)
- [Strategy Pattern for Storage Backends](https://refactoring.guru/design-patterns/strategy)

---

## 2. Optimized Heartbeat Write Strategy

### Decision
**Write to database ONLY on status changes or periodic snapshots** (every MaxMissedHeartbeats heartbeats), not every heartbeat.

### Rationale
- Target: 10,000 heartbeats/minute ≈ 167 heartbeats/second
- Writing every heartbeat creates excessive database load (10M writes/day)
- In-memory tracking sufficient for real-time health monitoring
- Database persistence needed only for:
  1. **Status transitions**: HEALTHY → UNHEALTHY → DEGRADED → DEAD
  2. **Periodic snapshots**: Every N heartbeats (configurable per service via MaxMissedHeartbeats)
- Reduces DB writes by 95%+ (typical service: 1 write/15 minutes vs 1 write/30 seconds)

### Implementation Pattern
```csharp
// In-memory heartbeat state per service
private readonly ConcurrentDictionary<Guid, HeartbeatState> heartbeatStates = new();

public async Task ProcessHeartbeatAsync(Guid serviceId, HeartbeatRequest request)
{
    HeartbeatState state = heartbeatStates.GetOrAdd(serviceId, _ => new HeartbeatState());
    
    state.LastHeartbeatTimestamp = DateTime.UtcNow;
    state.ConsecutiveSuccessCount++;
    state.HeartbeatsSinceLastWrite++;
    
    HealthStatus previousStatus = state.CurrentHealthStatus;
    HealthStatus newStatus = CalculateHealthStatus(state);
    
    bool shouldWrite = false;
    
    // Condition 1: Status changed
    if (newStatus != previousStatus)
    {
        shouldWrite = true;
        _logger.LogWarning("Service {ServiceId} status changed: {Previous} → {New}", 
            serviceId, previousStatus, newStatus);
    }
    
    // Condition 2: Periodic snapshot (every MaxMissedHeartbeats heartbeats)
    Service service = await _repository.GetByIdAsync(serviceId);
    if (state.HeartbeatsSinceLastWrite >= service.MaxMissedHeartbeats)
    {
        shouldWrite = true;
        state.HeartbeatsSinceLastWrite = 0;
    }
    
    if (shouldWrite)
    {
        service.HealthStatus = newStatus;
        service.LastHeartbeatTimestamp = state.LastHeartbeatTimestamp;
        service.HeartbeatCount += state.HeartbeatsSinceLastWrite;
        await _repository.UpdateAsync(service);
    }
}
```

### Alternatives Considered
- **Write every heartbeat**: Rejected due to database load (10M writes/day)
- **Batched writes every 5 seconds**: Still too frequent; status rarely changes
- **Write only on status changes**: Insufficient; need periodic snapshots for audit trail
- **Time-based periodic writes**: Less optimal than count-based (varies by service heartbeat frequency)

### References
- [Write-Behind Caching Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cache-aside)
- [Event-Driven Architecture](https://martinfowler.com/articles/201701-event-driven.html)

---

## 3. Service Name Normalization & Hashing Strategy

### Decision
Use **SHA256 hash of lowercase service name** for uniqueness validation, store both normalized hash and original name.

### Rationale
- Requirement R3: Normalize to lowercase for case-insensitive uniqueness
- Requirement R4: Store original name for display/debugging
- SHA256 provides strong collision resistance (negligible probability at 1,000 services)
- 64-character hex string suitable for NVARCHAR database column
- Built-in .NET cryptographic libraries, no external dependencies

### Implementation Pattern
```csharp
public class ServiceNameNormalizer
{
    public string NormalizeServiceName(string serviceName)
    {
        var lowercaseName = serviceName.ToLowerInvariant();
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(lowercaseName));
        return Convert.ToHexString(hashBytes); // Returns uppercase hex string
    }
}

// Database schema:
// ServiceNameNormalized NVARCHAR(64) NOT NULL UNIQUE
// ServiceName NVARCHAR(50) NOT NULL (original for display)
```

### Alternatives Considered
- **MD5 hash**: Rejected due to known collision vulnerabilities
- **Simple lowercase storage**: Rejected; doesn't provide idempotency key as specified
- **Case-sensitive uniqueness**: Rejected; conflicts with R3 requirement

### References
- [SHA256 in .NET](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256)

---

## 4. Background Service for Heartbeat Timeout Detection

### Decision
Use **BackgroundService with Timer-based polling** to detect missed heartbeats and update health status.

### Rationale
- IHostedService provides lifecycle management within ASP.NET Core
- BackgroundService simplifies long-running task implementation
- Timer-based polling (every 5-10 seconds) checks services due for heartbeat
- Configurable polling interval balances responsiveness vs DB load
- Graceful shutdown via CancellationToken

### Implementation Pattern
```csharp
public class HeartbeatMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HeartbeatMonitorService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Heartbeat monitor service starting");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var heartbeatService = scope.ServiceProvider.GetRequiredService<IHeartbeatService>();
                
                await heartbeatService.CheckForTimeoutsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking heartbeat timeouts");
            }
            
            await Task.Delay(_checkInterval, stoppingToken);
        }
        
        _logger.LogInformation("Heartbeat monitor service stopping");
    }
}

// Registration in Program.cs:
builder.Services.AddHostedService<HeartbeatMonitorService>();
```

### Alternatives Considered
- **Hangfire/Quartz.NET**: Rejected as overkill for simple periodic task
- **Database triggers**: Rejected; business logic should stay in application layer
- **Manual thread management**: Rejected in favor of framework-provided BackgroundService

### References
- [BackgroundService in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/timer-service)
- [Hosted Services in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)

---

## 5. API Rate Limiting & Throttling

### Decision
Use **ASP.NET Core 7.0+ built-in rate limiting middleware** with per-endpoint policies.

### Rationale
- Native framework support (Microsoft.AspNetCore.RateLimiting)
- Fixed window algorithm suitable for heartbeat endpoint (limit requests per time window)
- Sliding window for admin endpoints (better burst handling)
- Per-endpoint configuration allows different limits for heartbeat vs registration
- Standardized 429 (Too Many Requests) response

### Implementation Pattern
```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    // Heartbeat endpoint: fixed window, high limit
    options.AddFixedWindowLimiter("heartbeat", opt =>
    {
        opt.PermitLimit = 200; // 200 requests per window
        opt.Window = TimeSpan.FromSeconds(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 50;
    });
    
    // Registration endpoint: sliding window, lower limit
    options.AddSlidingWindowLimiter("registration", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 6;
    });
});

app.UseRateLimiter();

// Controller attribute:
[EnableRateLimiting("heartbeat")]
[HttpPost("api/v1/heartbeat/{serviceId}")]
public async Task<IActionResult> Heartbeat(string serviceId, HeartbeatRequestDto request) { }
```

### Alternatives Considered
- **AspNetCoreRateLimit library**: Rejected; prefer built-in framework features
- **API Gateway rate limiting**: Deferred to infrastructure; application should have own protection
- **Redis-based distributed rate limiting**: Deferred to P2 for multi-instance deployments

### References
- [Rate Limiting in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)

---

## 6. Soft-Delete Pattern with Storage Backend Abstraction

### Decision
Use **DeletionStatus enum** with storage-provider-specific query filtering.

### Rationale
- Requirement R30: Mark services as DELETED rather than physical deletion
- DeletionStatus enum: active, pending_deletion, deleted
- **PostgreSQL (EF Core)**: Global query filter automatically excludes deleted records
- **Redis**: Filter in repository layer (`services.Where(s => s.DeletionStatus != DeletionStatus.Deleted)`)
- **File**: Filter when loading from directory
- Maintains referential integrity and audit trail
- Supports R31: Re-registration with new ID (old record remains with DELETED status)

### Implementation Pattern
```csharp
public class Service
{
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; }
    public DeletionStatus DeletionStatus { get; set; } = DeletionStatus.Active;
    public DateTime? DeletionRequestedDate { get; set; }
    public string DeletionRequestedBy { get; set; }
    // ... other properties
}

public enum DeletionStatus
{
    Active = 0,
    PendingDeletion = 1,
    Deleted = 2
}

// PostgresStorageProvider: EF Core DbContext configuration
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Service>()
        .HasQueryFilter(s => s.DeletionStatus != DeletionStatus.Deleted);
    
    // Unique constraint only for non-deleted services
    modelBuilder.Entity<Service>()
        .HasIndex(s => s.ServiceNameNormalized)
        .IsUnique()
        .HasFilter("[DeletionStatus] != 2"); // SQL Server/PostgreSQL syntax
}

// RedisStorageProvider: Filter in repository implementation
public async Task<List<Service>> GetByHealthStatusAsync(HealthStatus status)
{
    List<Service> allServices = await GetAllServicesAsync();
    return allServices
        .Where(s => s.DeletionStatus != DeletionStatus.Deleted)
        .Where(s => s.HealthStatus == status)
        .ToList();
}
```

### Alternatives Considered
- **IsDeleted boolean flag**: Rejected; status enum provides richer state (active, pending, deleted)
- **Deleted date only**: Rejected; doesn't capture pending deletion state
- **Separate archive table**: Rejected; increases complexity and breaks foreign keys

### Storage Provider Considerations
- **SQL databases**: Use query filters at database level (efficient)
- **Redis/NoSQL**: Filter in application layer after retrieval
- **File**: Filter when loading from directory structure

### References
- [EF Core Global Query Filters](https://learn.microsoft.com/en-us/ef/core/querying/filters)
- [Soft Delete Pattern](https://learn.microsoft.com/en-us/ef/core/miscellaneous/nullable-reference-types#required-navigation)

---

## 7. Concurrent Registration Handling

### Decision
Use **unique constraint on ServiceNameNormalized** with storage-provider-specific concurrency handling.

### Rationale
- Requirement R5: Return existing registration ID if pending registration exists for same name
- Unique constraint enforces idempotency
- **PostgreSQL**: Catch DbUpdateException on constraint violation, query existing registration
- **Redis**: Check existence before write (atomic GET + SET if not exists via Lua script)
- **File**: Lock file when checking existence (FileStream with exclusive access)
- **Optimistic concurrency**: 
  - SQL databases: RowVersion/Timestamp column (last write wins for Redis/File)
  - Prevents lost updates during status transitions

### Implementation Pattern
```csharp
// IServiceRepository implementation
public async Task<RegistrationResponse> RegisterServiceAsync(RegistrationRequest request)
{
    string normalizedName = _normalizer.NormalizeServiceName(request.ServiceName);
    
    // Attempt to create new registration via storage provider
    RegistrationRequest registration = new RegistrationRequest
    {
        RegistrationId = Guid.NewGuid(),
        ServiceName = request.ServiceName,
        ServiceNameNormalized = normalizedName,
        Status = RegistrationStatus.Pending,
        // ... other fields
    };
    
    bool created = await _storageProvider.CreateIfNotExistsAsync(
        key: $"registration:{normalizedName}",
        entity: registration);
    
    if (!created)
    {
        // Idempotency: return existing pending registration
        RegistrationRequest? existing = await _storageProvider.ReadAsync<RegistrationRequest>(
            $"registration:{normalizedName}");
        
        if (existing?.Status == RegistrationStatus.Pending)
        {
            return new RegistrationResponse 
            { 
                RegistrationId = existing.RegistrationId, 
                Status = "pending" 
            };
        }
    }
    
    return new RegistrationResponse 
    { 
        RegistrationId = registration.RegistrationId, 
        Status = "pending" 
    };
}

// PostgreSQL: Add RowVersion for optimistic concurrency
public class Service
{
    [Timestamp] // Only used for SQL databases
    public byte[]? RowVersion { get; set; }
}

// Redis: Lua script for atomic check-and-set
string luaScript = @"
    if redis.call('exists', KEYS[1]) == 0 then
        redis.call('set', KEYS[1], ARGV[1])
        return 1
    else
        return 0
    end
";
```

### Alternatives Considered
- **Distributed lock**: Rejected; storage-level atomicity sufficient
- **Pessimistic locking (SQL SELECT FOR UPDATE)**: Rejected; optimistic approach has better throughput
- **Check-then-insert pattern**: Rejected; race condition between check and insert

### References
- [EF Core Concurrency Tokens](https://learn.microsoft.com/en-us/ef/core/modeling/concurrency)
- [Handling DbUpdateException](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)

---

## 8. Health Status State Machine Implementation

### Decision
Use **explicit state transition methods with validation** rather than state pattern class hierarchy.

### Rationale
- Six states: HEALTHY, UNHEALTHY, DEGRADED, DEAD, RECOVERED, DELETED
- Transitions defined in requirements R36-R46 are deterministic and rule-based
- Service class encapsulates state + transition logic methods
- No need for polymorphic behavior (State pattern overkill)
- Transition validation prevents invalid state changes
- Audit log for all transitions per R50

### Implementation Pattern
```csharp
public enum HealthStatus
{
    Healthy = 0,
    Unhealthy = 1,
    Degraded = 2,
    Dead = 3,
    Recovered = 4,
    Deleted = 5
}

public class Service
{
    public HealthStatus HealthStatus { get; private set; }
    public int MissedHeartbeatCounter { get; private set; }
    public int MaxMissedHeartbeats { get; set; }
    
    public void ProcessSuccessfulHeartbeat(ILogger logger)
    {
        var previousStatus = HealthStatus;
        
        switch (HealthStatus)
        {
            case HealthStatus.Healthy:
            case HealthStatus.Unhealthy:
            case HealthStatus.Degraded:
                HealthStatus = HealthStatus.Healthy;
                MissedHeartbeatCounter = 0;
                break;
                
            case HealthStatus.Dead:
                HealthStatus = HealthStatus.Recovered;
                MissedHeartbeatCounter = 0;
                break;
                
            case HealthStatus.Recovered:
                // Count consecutive successful heartbeats
                MissedHeartbeatCounter--;
                if (MissedHeartbeatCounter <= -MaxMissedHeartbeats)
                {
                    HealthStatus = HealthStatus.Healthy;
                    MissedHeartbeatCounter = 0;
                }
                break;
        }
        
        if (previousStatus != HealthStatus)
        {
            logger.LogInformation(
                "Service {ServiceId} health status changed from {PreviousStatus} to {NewStatus}",
                ServiceId, previousStatus, HealthStatus);
        }
    }
    
    public void ProcessMissedHeartbeat(ILogger logger)
    {
        var previousStatus = HealthStatus;
        MissedHeartbeatCounter++;
        
        if (MissedHeartbeatCounter == 1)
        {
            HealthStatus = HealthStatus.Unhealthy;
        }
        else if (MissedHeartbeatCounter >= MaxMissedHeartbeats / 2.0)
        {
            HealthStatus = HealthStatus.Degraded;
        }
        
        if (MissedHeartbeatCounter > MaxMissedHeartbeats)
        {
            HealthStatus = HealthStatus.Dead;
        }
        
        if (previousStatus != HealthStatus)
        {
            logger.LogWarning(
                "Service {ServiceId} health status degraded from {PreviousStatus} to {NewStatus}, missed heartbeats: {Counter}",
                ServiceId, previousStatus, HealthStatus, MissedHeartbeatCounter);
        }
    }
}
```

### Alternatives Considered
- **State pattern with classes**: Rejected; 6 state classes + service class increases complexity
- **External state machine library (Stateless)**: Rejected; simple rules don't warrant library
- **Event sourcing**: Rejected; not required for current audit trail needs

### References
- [State Pattern](https://refactoring.guru/design-patterns/state)
- [Domain-Driven Design: Aggregate State Transitions](https://martinfowler.com/bliki/DomainDrivenDesign.html)

---

## 9. Blazor Server vs Blazor WebAssembly for Dashboard

### Decision
Use **Blazor Server** for the dashboard implementation.

### Rationale
- Real-time updates via SignalR built into Blazor Server architecture
- Server-side rendering reduces client payload and browser requirements
- Direct access to backend services without additional API layer
- Authentication/authorization handled server-side (aligns with P2 deferred auth)
- Lower latency for database queries (no client-server round-trips for data)
- Dashboard users (admins, service owners) are internal users with stable network connections

### Implementation Pattern
```csharp
// Program.cs
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

// Real-time hub for dashboard updates
public class ServiceMonitorHub : Hub
{
    public async Task SubscribeToServiceUpdates()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "ServiceMonitoring");
    }
}

// Background service pushes updates via SignalR
public class HealthStatusBroadcaster : BackgroundService
{
    private readonly IHubContext<ServiceMonitorHub> _hubContext;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // When health status changes, broadcast to connected clients
        await _hubContext.Clients.Group("ServiceMonitoring")
            .SendAsync("HealthStatusChanged", serviceId, newStatus, stoppingToken);
    }
}
```

### Alternatives Considered
- **Blazor WebAssembly**: Rejected; requires separate API for all data access, adds complexity without benefit for internal dashboard
- **Traditional MVC/Razor Pages**: Rejected; less interactive, requires more JavaScript for real-time updates
- **React/Angular SPA**: Rejected; introduces JavaScript ecosystem, team uses C# expertise

### References
- [Blazor Hosting Models](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models)
- [SignalR with Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor)

---

## 10. Real-time Dashboard Updates

### Decision
Use **SignalR hub with server-side push notifications** for real-time health status updates.

### Rationale
- Requirement R53: Near-real-time dashboard updates
- Success Criteria: Health status changes reflected within 10 seconds
- SignalR automatically handles connection management and reconnection
- Push model more efficient than polling for idle dashboards
- Background service broadcasts changes when health status transitions occur
- Clients subscribe to specific update channels (all services vs owned services)

### Implementation Pattern
```csharp
// Blazor component subscribing to updates
@page "/monitoring"
@inject IHubConnection HubConnection

<ServiceTable Services="@services" />

@code {
    private List<ServiceViewModel> services;
    
    protected override async Task OnInitializedAsync()
    {
        services = await LoadServicesAsync();
        
        HubConnection.On<Guid, string>("HealthStatusChanged", (serviceId, newStatus) =>
        {
            var service = services.FirstOrDefault(s => s.ServiceId == serviceId);
            if (service != null)
            {
                service.HealthStatus = newStatus;
                StateHasChanged(); // Re-render component
            }
        });
        
        await HubConnection.StartAsync();
        await HubConnection.SendAsync("SubscribeToServiceUpdates");
    }
}
```

### Alternatives Considered
- **Polling (every 5-10 seconds)**: Rejected; wastes bandwidth when no changes, adds database load
- **Server-Sent Events (SSE)**: Rejected; SignalR provides better fallback mechanisms and tooling
- **WebSockets only**: Rejected; SignalR handles fallback to long-polling when WebSockets unavailable

### References
- [SignalR Hub Methods](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs)
- [Blazor SignalR Integration](https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor)

---

## 11. Docker Multi-Stage Build Optimization

### Decision
Use **multi-stage Dockerfile with SDK and runtime separation** for optimized container images.

### Rationale
- Requirement R57: Optimized image size
- SDK stage builds application (large, contains compilers and tools)
- Runtime stage contains only published app and runtime (small, production-ready)
- Non-root user execution for security
- Health check endpoint for container orchestration
- Typical image size: 200-300MB (runtime) vs 1.5GB+ (SDK included)

### Implementation Pattern
```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["src/Omni.ServiceRegistry.Api/Omni.ServiceRegistry.Api.csproj", "src/Omni.ServiceRegistry.Api/"]
COPY ["src/Omni.ServiceRegistry.Dashboard/Omni.ServiceRegistry.Dashboard.csproj", "src/Omni.ServiceRegistry.Dashboard/"]
# Copy other project files...
RUN dotnet restore "src/Omni.ServiceRegistry.Api/Omni.ServiceRegistry.Api.csproj"
COPY . .
WORKDIR "/src/src/Omni.ServiceRegistry.Api"
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Omni.ServiceRegistry.Api.dll"]
```

### Alternatives Considered
- **Single-stage build**: Rejected; results in 1.5GB+ images with unnecessary SDK tools
- **Separate Dockerfile per project**: Rejected; increases maintenance, prefer single multi-stage build
- **Alpine base images**: Considered; slightly smaller but may have compatibility issues with some NuGet packages

### References
- [Docker Multi-Stage Builds](https://docs.docker.com/build/building/multi-stage/)
- [.NET Docker Best Practices](https://learn.microsoft.com/en-us/dotnet/core/docker/build-container)

---

## 12. Kubernetes Horizontal Pod Autoscaling

### Decision
Use **Kubernetes HPA with CPU-based autoscaling** for API pods, manual scaling for database.

### Rationale
- Requirement R61: Support horizontal scaling
- Target: 10,000 heartbeats/minute, 1,000 services, 50 concurrent users
- CPU utilization reliable metric for stateless API workload
- HPA automatically adjusts replica count based on average CPU across pods
- Min replicas: 2 (high availability), Max replicas: 10 (cost control)
- Target CPU: 70% (balance between responsiveness and resource efficiency)

### Implementation Pattern
```yaml
# hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: serviceregistry-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: serviceregistry-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300  # Wait 5 minutes before scaling down
    scaleUp:
      stabilizationWindowSeconds: 0    # Scale up immediately
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
```

### Alternatives Considered
- **Custom metrics (heartbeat rate)**: Deferred to future; requires metrics server and adapter setup
- **Memory-based autoscaling**: Rejected; CPU more predictive for this workload
- **Fixed replica count**: Rejected; wastes resources during low usage, insufficient during peak

### References
- [Kubernetes HPA](https://kubernetes.io/docs/tasks/run-application/horizontal-pod-autoscale/)
- [HPA Walkthrough](https://kubernetes.io/docs/tasks/run-application/horizontal-pod-autoscale-walkthrough/)

---

## 13. Database Migrations in Kubernetes

### Decision
Use **Kubernetes Job with EF Core migrations** executed before application deployment.

### Rationale
- Requirement R60: EF Core migrations for schema management
- Job runs once per deployment, applies pending migrations
- Idempotent: EF Core tracks applied migrations in `__EFMigrationsHistory` table
- Fails deployment if migration fails (prevents broken schema + app mismatch)
- Separate job allows database initialization before API pods start
- Supports rollback: can apply down migrations via manual job

### Implementation Pattern
```yaml
# migration-job.yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: serviceregistry-migration-{{ .Values.deployment.version }}
spec:
  backoffLimit: 3
  template:
    spec:
      restartPolicy: Never
      containers:
      - name: migration
        image: serviceregistry:{{ .Values.deployment.version }}
        command: ["dotnet", "ef", "database", "update"]
        env:
        - name: ConnectionStrings__ServiceRegistry
          valueFrom:
            secretKeyRef:
              name: serviceregistry-secrets
              key: database-connection-string
      initContainers:
      - name: wait-for-db
        image: busybox:1.28
        command: ['sh', '-c', 'until nc -z postgres-service 5432; do sleep 2; done']
```

### Helm pre-upgrade hook alternative:
```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: serviceregistry-migration
  annotations:
    "helm.sh/hook": pre-upgrade,pre-install
    "helm.sh/hook-weight": "0"
    "helm.sh/hook-delete-policy": before-hook-creation
```

### Alternatives Considered
- **Init container in API deployment**: Rejected; all pods run migration concurrently, causing locks
- **Manual migration execution**: Rejected; increases deployment friction, error-prone
- **Bundled migration on app startup**: Rejected; causes startup delays, race conditions with multiple replicas

### References
- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Kubernetes Jobs](https://kubernetes.io/docs/concepts/workloads/controllers/job/)

---

## 14. Redis as Storage Backend with JSON Module

### Decision
Use **RedisJSON module** for document-based entity storage with indexing capabilities.

### Rationale
- Redis serves as alternative storage backend (not cache layer)
- RedisJSON provides native JSON document support with path-based queries
- Indexing via RediSearch enables efficient queries by health status, owner, etc.
- Atomic operations for concurrency control (Lua scripts for check-and-set)
- StatefulSet deployment with RDB snapshots for persistence
- Simpler data model than Redis Hashes (stores entire entity as JSON document)

### Implementation Pattern
```csharp
public class RedisStorageProvider : IStorageProvider
{
    private readonly IDatabase _redis;
    
    public async Task WriteAsync<T>(string key, T entity) where T : class
    {
        string json = JsonSerializer.Serialize(entity);
        await _redis.JsonSetAsync(key, "$", json);
    }
    
    public async Task<T?> ReadAsync<T>(string key) where T : class
    {
        RedisValue json = await _redis.JsonGetAsync(key);
        if (json.IsNull) return null;
        return JsonSerializer.Deserialize<T>(json.ToString());
    }
    
    public async Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate) where T : class
    {
        // Use RediSearch index for efficient queries
        // Fall back to SCAN + filter for complex predicates
        List<T> results = new();
        await foreach (RedisKey key in _redis.Server.KeysAsync(pattern: "service:*"))
        {
            T? entity = await ReadAsync<T>(key);
            if (entity != null && predicate(entity))
            {
                results.Add(entity);
            }
        }
        return results;
    }
}

// Key naming convention
// - Registration: "registration:{normalizedName}" → RegistrationRequest JSON
// - Service: "service:{serviceId}" → Service JSON
// - Index: "service:index:owner:{email}" → Set of serviceId values
```

### Data Persistence
```yaml
# Redis StatefulSet with RDB persistence
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: redis
spec:
  serviceName: redis
  template:
    spec:
      containers:
      - name: redis
        image: redis/redis-stack-server:latest  # Includes RedisJSON + RediSearch
        command:
        - redis-server
        - "--save 900 1"      # RDB snapshot: save after 900s if ≥1 key changed
        - "--save 300 10"     # RDB snapshot: save after 300s if ≥10 keys changed
        - "--save 60 10000"   # RDB snapshot: save after 60s if ≥10000 keys changed
        volumeMounts:
        - name: redis-data
          mountPath: /data
  volumeClaimTemplates:
  - metadata:
      name: redis-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 10Gi
```

### Alternatives Considered
- **Redis Hashes**: Rejected; requires field-by-field mapping, less natural than JSON documents
- **Redis Strings with JSON serialization**: Rejected; no native query support, requires full scan
- **Redis Streams**: Rejected; designed for event logs, not entity storage
- **No persistence (cache-only)**: Rejected; Redis is primary storage option, must survive restarts

### Trade-offs
- **Pros**: Simple deployment, high performance reads/writes, native JSON support
- **Cons**: 
  - No foreign keys or referential integrity (enforced in application layer)
  - Complex queries less efficient than SQL (requires scanning or secondary indexes)
  - No built-in migrations (schema evolution handled in code)

### References
- [RedisJSON Documentation](https://redis.io/docs/stack/json/)
- [RediSearch for Indexing](https://redis.io/docs/stack/search/)
- [Redis Persistence](https://redis.io/docs/management/persistence/)

---

## Summary of Decisions

| Topic | Decision | Key Library/Pattern |
|-------|----------|-------------------|
| Storage Backend | Pluggable abstraction with multiple implementations | IStorageProvider, IServiceRepository |
| Heartbeat Writes | Write only on status change or periodic snapshot | In-memory state, conditional DB writes |
| Service Name Normalization | SHA256 hash of lowercase name | System.Security.Cryptography.SHA256 |
| Timeout Detection | BackgroundService with timer | BackgroundService, Timer |
| Rate Limiting | Built-in ASP.NET Core middleware | Microsoft.AspNetCore.RateLimiting |
| Soft Delete | Status enum with provider-specific filtering | EF Core query filters, application-layer filtering |
| Concurrent Registration | Unique constraint + atomic check-and-set | EF Core constraints, Redis Lua scripts |
| State Machine | Explicit transition methods in Service entity | Domain model encapsulation |
| Dashboard Hosting | Blazor Server | SignalR, server-side rendering |
| Real-time Updates | SignalR hub with push notifications | IHubContext, Groups |
| Container Image | Multi-stage Docker build | SDK build stage, runtime stage |
| Horizontal Scaling | Kubernetes HPA with CPU metrics | HPA, min 2 / max 10 replicas |
| Database Migrations | Kubernetes Job with EF Core (SQL only) | Pre-deployment Job, idempotent migrations |
| Redis Storage | RedisJSON module with RDB persistence | redis-stack-server, RedisJSON, StatefulSet |

## Performance Implications

- **Heartbeat throughput**: Optimized write strategy reduces DB load by 95%+, enables 10,000+ heartbeats/minute
- **Storage flexibility**: Operator chooses backend (Postgres for ACID, Redis for speed, File for testing)
- **Rate limiting**: Protects against abuse without impacting legitimate traffic
- **Query performance**: 
  - SQL: Indexes on ServiceNameNormalized, HealthStatus, LastHeartbeatTimestamp
  - Redis: RediSearch indexes + key patterns for efficient queries
- **Background service**: 5-second polling interval balances responsiveness vs DB load

## Next Steps

1. ✅ Research complete (14 topics)
2. ⏳ Proceed to Phase 1: Design artifacts (data-model.md, contracts/, quickstart.md)
3. ⏳ Update data model with storage-agnostic patterns
4. ⏳ Update deployment artifacts for Redis option
5. ⏳ Update agent context with storage abstraction patterns
