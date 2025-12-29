# Quick Reference: New Features

## Service Deletion API

### Request Deletion
```bash
curl -X POST http://localhost:8080/api/v1/admin/services/{serviceId}/delete \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Service is being decommissioned",
    "comments": "Migrate to new-payment-service before deletion"
  }'
```

### Get Pending Deletions
```bash
curl http://localhost:8080/api/v1/admin/deletions/pending
```

### Approve Deletion
```bash
curl -X POST http://localhost:8080/api/v1/admin/deletions/{serviceId}/approve
```

---

## Kubernetes Deployment

### Deploy Everything
```bash
cd deployment/kubernetes

# Step 1: Create namespace and configuration
kubectl apply -f 00-namespace.yaml
kubectl apply -f 01-configmap.yaml
kubectl apply -f 02-secret.yaml  # ⚠️ Update passwords first!

# Step 2: Deploy databases
kubectl apply -f 03-postgres.yaml
kubectl apply -f 04-redis.yaml

# Step 3: Wait for databases
kubectl wait --for=condition=ready pod -l app=postgres -n service-registry --timeout=120s
kubectl wait --for=condition=ready pod -l app=redis -n service-registry --timeout=120s

# Step 4: Run migrations
kubectl apply -f 05-migration-job.yaml
kubectl wait --for=condition=complete job/db-migration -n service-registry --timeout=300s

# Step 5: Deploy applications
kubectl apply -f 06-api-deployment.yaml
kubectl apply -f 07-dashboard-deployment.yaml
kubectl apply -f 08-hpa.yaml

# Step 6: (Optional) Setup ingress
kubectl apply -f 09-ingress.yaml
```

### Check Status
```bash
kubectl get pods -n service-registry
kubectl get svc -n service-registry
kubectl get hpa -n service-registry
```

### Access Services (Port Forward)
```bash
# API
kubectl port-forward -n service-registry svc/api-service 8080:80

# Dashboard
kubectl port-forward -n service-registry svc/dashboard-service 8081:80
```

### Scale Manually
```bash
kubectl scale deployment service-registry-api -n service-registry --replicas=5
```

### View Logs
```bash
kubectl logs -l app=service-registry-api -n service-registry --tail=50 -f
```

---

## Docker Deployment

### Build Images
```bash
cd deployment/docker

# Build API
docker build -f Dockerfile.api -t service-registry-api:latest ../..

# Build Dashboard
docker build -f Dockerfile.dashboard -t service-registry-dashboard:latest ../..
```

### Run with Docker Compose (PostgreSQL)
```bash
docker-compose --profile postgres up --build
```

### Run with Docker Compose (Redis)
```bash
docker-compose --profile redis up --build
```

### Access
- API: http://localhost:8080
- Dashboard: http://localhost:5160
- PostgreSQL: localhost:5432
- Redis: localhost:6379

### Stop
```bash
docker-compose --profile postgres down
```

---

## Redis Storage Configuration

### API appsettings.json
```json
{
  "DatabaseProvider": "Redis",
  "ConnectionStrings": {
    "ServiceRegistry": "localhost:6379"
  }
}
```

### Dashboard appsettings.json
```json
{
  "DatabaseProvider": "Redis",
  "ConnectionStrings": {
    "ServiceRegistry": "localhost:6379"
  }
}
```

### Run Redis with Docker
```bash
docker run -d -p 6379:6379 --name redis redis/redis-stack-server:latest
```

---

## SignalR Real-time Updates

### Dashboard Hub Connection (JavaScript)
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/monitoring")
    .build();

// Subscribe to health status changes
connection.on("HealthStatusChanged", (serviceId, serviceName, newStatus) => {
    console.log(`${serviceName} status changed to ${newStatus}`);
    // Update UI
});

// Subscribe to new service registrations
connection.on("ServiceRegistered", (serviceId, serviceName) => {
    console.log(`New service registered: ${serviceName}`);
});

// Subscribe to service deletions
connection.on("ServiceDeleted", (serviceId, serviceName) => {
    console.log(`Service deleted: ${serviceName}`);
});

// Start connection
await connection.start();
```

### Blazor Component (C#)
```csharp
@inject NavigationManager Navigation
@implements IAsyncDisposable

<h3>Real-time Service Monitor</h3>

@code {
    private HubConnection? hubConnection;

    protected override async Task OnInitializedAsync()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/hubs/monitoring"))
            .Build();

        hubConnection.On<Guid, string, string>("HealthStatusChanged", 
            async (id, name, status) => {
                // Update state
                await InvokeAsync(StateHasChanged);
            });

        await hubConnection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }
}
```

---

## Dashboard Components Usage

### ServiceCard Component
```razor
@using Omni.ServiceRegistry.Dashboard.Components

<ServiceCard 
    Service="@serviceViewModel"
    ShowDetailsButton="true"
    OnDetailsClick="NavigateToDetails" />

@code {
    private ServiceCard.ServiceViewModel serviceViewModel = new()
    {
        ServiceId = Guid.NewGuid(),
        ServiceName = "payment-service",
        Description = "Payment processing",
        ContactEmail = "team@example.com",
        Endpoints = "[\"https://api.example.com/payments\"]",
        HealthStatus = "Healthy",
        LastHeartbeatTimestamp = DateTime.UtcNow.AddMinutes(-2),
        HeartbeatCount = 1542
    };

    private void NavigateToDetails(Guid serviceId)
    {
        NavigationManager.NavigateTo($"/service/{serviceId}");
    }
}
```

### HealthStatusOverview Component
```razor
@using Omni.ServiceRegistry.Dashboard.Components

<HealthStatusOverview 
    TotalCount="@services.Count"
    HealthyCount="@services.Count(s => s.HealthStatus == \"Healthy\")"
    UnhealthyCount="@services.Count(s => s.HealthStatus == \"Unhealthy\")"
    DegradedCount="@services.Count(s => s.HealthStatus == \"Degraded\")"
    DeadCount="@services.Count(s => s.HealthStatus == \"Dead\")"
    RecoveredCount="@services.Count(s => s.HealthStatus == \"Recovered\")" />
```

### SearchBox Component
```razor
@using Omni.ServiceRegistry.Dashboard.Components

<SearchBox 
    Placeholder="Search services by name or description..."
    SearchTerm="@searchTerm"
    SearchTermChanged="HandleSearchChanged" />

@code {
    private string searchTerm = "";

    private async Task HandleSearchChanged(string newSearchTerm)
    {
        searchTerm = newSearchTerm;
        // Filter services
        await FilterServices();
    }
}
```

---

## Environment Variables

### API Container
```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
DatabaseProvider=Postgres  # or Redis, InMemory
ConnectionStrings__ServiceRegistry=Host=postgres;Port=5432;Database=serviceregistry;Username=postgres;Password=yourpassword
HeartbeatMonitoring__CheckIntervalSeconds=5
RateLimiting__HeartbeatPermitLimit=200
Logging__LogLevel__Default=Information
```

### Dashboard Container
```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
DatabaseProvider=Postgres
ConnectionStrings__ServiceRegistry=Host=postgres;Port=5432;Database=serviceregistry;Username=postgres;Password=yourpassword
Logging__LogLevel__Default=Information
```

---

## Monitoring & Troubleshooting

### Check Health
```bash
# API health check
curl http://localhost:8080/health

# Dashboard health check
curl http://localhost:5160/health
```

### View Metrics (Kubernetes)
```bash
# Pod resource usage
kubectl top pods -n service-registry

# Node resource usage
kubectl top nodes

# HPA status
kubectl get hpa -n service-registry -w
```

### Debug Connection Issues
```bash
# Test PostgreSQL connection
kubectl exec -it postgres-0 -n service-registry -- psql -U postgres -d serviceregistry -c "SELECT COUNT(*) FROM \"Services\";"

# Test Redis connection
kubectl exec -it redis-0 -n service-registry -- redis-cli ping
```

### Common Issues

**Pod Not Starting**
```bash
kubectl describe pod <pod-name> -n service-registry
kubectl logs <pod-name> -n service-registry
```

**Database Connection Failed**
```bash
# Check database pod is running
kubectl get pods -l app=postgres -n service-registry

# Check secret is configured
kubectl get secret service-registry-secrets -n service-registry -o yaml
```

**Migration Job Failed**
```bash
kubectl logs job/db-migration -n service-registry
kubectl delete job db-migration -n service-registry
kubectl apply -f 05-migration-job.yaml
```

---

## Testing Checklist

### Service Deletion Flow
- [ ] Request deletion via API
- [ ] Verify status = PendingDeletion
- [ ] Check pending deletions list
- [ ] Approve deletion
- [ ] Verify status = Deleted
- [ ] Confirm service excluded from queries
- [ ] Try re-registering same name (should work)

### Kubernetes Deployment
- [ ] All pods running
- [ ] Health checks passing
- [ ] HPA configured correctly
- [ ] Can access API via port-forward
- [ ] Can access Dashboard via port-forward
- [ ] Database migrations applied
- [ ] Logs show no errors

### Docker Deployment
- [ ] Images build successfully
- [ ] Containers start without errors
- [ ] API accessible on localhost:8080
- [ ] Dashboard accessible on localhost:5160
- [ ] PostgreSQL container healthy
- [ ] Can register a service
- [ ] Can approve registration

### Redis Storage
- [ ] API starts with Redis provider
- [ ] Can register service
- [ ] Can approve registration
- [ ] Can submit heartbeat
- [ ] Services persisted after restart
- [ ] Queries return correct data

### SignalR
- [ ] Hub endpoint accessible (/hubs/monitoring)
- [ ] Can establish connection
- [ ] Can subscribe to events
- [ ] (Future) Receives notifications

### Dashboard Components
- [ ] ServiceCard displays correctly
- [ ] HealthStatusOverview shows counts
- [ ] SearchBox filters results
- [ ] Components responsive
- [ ] Icons display properly

---

## Performance Tuning

### Increase Heartbeat Throughput
```json
{
  "RateLimiting": {
    "HeartbeatPermitLimit": 500,  // Increase from 200
    "HeartbeatQueueLimit": 100     // Increase from 50
  }
}
```

### Scale API Replicas
```bash
# Kubernetes
kubectl scale deployment service-registry-api -n service-registry --replicas=5

# Docker Compose
docker-compose up --scale api=3
```

### Optimize PostgreSQL
```sql
-- Check slow queries
SELECT * FROM pg_stat_statements ORDER BY total_exec_time DESC LIMIT 10;

-- Analyze tables
ANALYZE "Services";
ANALYZE "RegistrationRequests";
```

### Redis Memory Configuration
```bash
# Set maxmemory policy
redis-cli CONFIG SET maxmemory-policy allkeys-lru
redis-cli CONFIG SET maxmemory 512mb
```

---

## Security Hardening

### Update Secrets (Production)
```bash
# Generate strong password
openssl rand -base64 32

# Update Kubernetes secret
kubectl create secret generic service-registry-secrets \
  --from-literal=postgres-connection="Host=postgres-service;Port=5432;Database=serviceregistry;Username=postgres;Password=$(openssl rand -base64 32)" \
  -n service-registry \
  --dry-run=client -o yaml | kubectl apply -f -
```

### Enable TLS
```yaml
# Update ingress with cert-manager
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
  - hosts:
    - api.service-registry.example.com
    secretName: service-registry-tls
```

### Network Policies
```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: api-network-policy
  namespace: service-registry
spec:
  podSelector:
    matchLabels:
      app: service-registry-api
  policyTypes:
  - Ingress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: service-registry-dashboard
    ports:
    - protocol: TCP
      port: 8080
```

---

## Support & Documentation

- Main README: `/README.md`
- Implementation Details: `/IMPLEMENTATION_COMPLETE.md`
- What's Not Implemented: `/WHATS_NOT_IMPLEMENTED.md`
- Kubernetes Guide: `/deployment/kubernetes/README.md`
- API Specification: `/specs/1-register-service/spec.md`
