# Quick Reference

## Quick Start Commands

### Start Everything (Recommended)
```bash
./quickstart.sh
```
This starts PostgreSQL, Ollama with phi4 model, API (port 5159), and Dashboard (port 5083).

### Access Points
- Dashboard: http://localhost:5083
- API: http://localhost:5159
- API Docs: http://localhost:5159/scalar/v1
- Ollama: http://localhost:11434

---

## AI Health Insights

### Trigger Global Analysis
```bash
# Via Dashboard: Click "✨ AI Analysis" button on main page

# Via API:
curl -X POST http://localhost:5159/api/v1/insights/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "globalAnalysis": true
  }'
```

### Get Recent Insights
```bash
curl http://localhost:5159/api/v1/insights/recent?count=20
```

### Get Service Insights
```bash
curl http://localhost:5159/api/v1/insights/service/{serviceId}
```

### Check Ollama Health
```bash
curl http://localhost:5159/api/v1/insights/health/ollama
```
---

## Service Registration & Management

### Register a Service
```bash
curl -X POST http://localhost:5159/api/v1/register \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "payment-service",
    "description": "Payment processing",
    "contactEmail": "team@example.com",
    "endpoints": ["http://payment.internal:8080"],
    "apiEndpoints": [
      {
        "method": "POST",
        "path": "/api/payments",
        "description": "Process payment"
      }
    ],
    "heartbeatTimeout": 30,
    "maxMissedHeartbeats": 5
  }'
```

### Send Heartbeat
```bash
curl -X POST http://localhost:5159/api/v1/heartbeat/{serviceId} \
  -H "Content-Type: application/json" \
  -d '{"metadata": {"version": "1.2.3"}}'
```

### Get Service Status
```bash
curl http://localhost:5159/api/v1/status/service/{serviceId}
```

---

## Service Deletion

### Request Deletion
```bash
curl -X POST http://localhost:5159/api/v1/delete/{serviceId} \
  -H "Content-Type: application/json" \
  -d '{
    "requestedBy": "admin@example.com",
    "reason": "Service decommissioned",
    "comments": "Migrate to new service first"
  }'
```

### Approve Deletion
```bash
curl -X POST http://localhost:5159/api/v1/admin/deletions/{serviceId}/approve \
  -H "Content-Type: application/json" \
  -d '{
    "approvedBy": "admin@example.com"
  }'
```

### Get Pending Deletions
```bash
curl http://localhost:5159/api/v1/admin/deletions/pending
```

### Restore Deleted Service
```bash
curl -X POST http://localhost:5159/api/v1/admin/services/{serviceId}/restore \
  -H "Content-Type: application/json" \
  -d '{
    "restoredBy": "admin@example.com",
    "reason": "Service needed again",
    "restoreMethod": "Manual"
  }'
```

---

## Administrator Actions

### Approve Registration
```bash
curl -X POST http://localhost:5159/api/v1/admin/registrations/{id}/approve \
  -H "Content-Type: application/json" \
  -d '{
    "approvedBy": "admin@example.com",
    "comments": "Verified requirements"
  }'
```

### Deny Registration
```bash
curl -X POST http://localhost:5159/api/v1/admin/registrations/{id}/deny \
  -H "Content-Type: application/json" \
  -d '{
    "deniedBy": "admin@example.com",
    "reason": "Missing documentation"
  }'
```

### Validate Service Endpoints
```bash
curl -X POST http://localhost:5159/api/v1/admin/registrations/{id}/validate
```

### Bulk Approve Registrations
Use the Dashboard: Navigate to Pending Approvals → Click "Approve All" button

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

## Docker Deployment (Deprecated)

Manual Docker deployment is deprecated. Use `./quickstart.sh` for local development.

---

## Database Configuration

### PostgreSQL (Default)
```json
{
  "DatabaseProvider": "Postgres",
  "ConnectionStrings": {
    "ServiceRegistry": "Host=localhost;Database=serviceregistry;Username=serviceregistry;Password=dev_password"
  }
}
```

### In-Memory (Testing Only)
```json
{
  "DatabaseProvider": "InMemory"
}
```

---

## Ollama Configuration

### Default Settings (appsettings.json)
```json
{
  "HealthInsights": {
    "OllamaBaseUrl": "http://localhost:11434",
    "OllamaModel": "phi4",
    "MaxTokens": 4500,
    "Temperature": 0.7,
    "EnableAnalysis": true
  }
}
```

### Pull phi4 Model Manually
```bash
ollama pull phi4
```

### Check Ollama Status
```bash
curl http://localhost:11434/
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

- **Main README**: [/README.md](/README.md)
- **Client Simulator**: [CLIENT_SIMULATOR_GUIDE.md](CLIENT_SIMULATOR_GUIDE.md)
- **What's Not Implemented**: [WHATS_NOT_IMPLEMENTED.md](WHATS_NOT_IMPLEMENTED.md)
- **Deployment Guide**: [DEPLOYMENT.md](DEPLOYMENT.md)
- **Architecture**: [/.github/copilot-instructions.md](/.github/copilot-instructions.md)

---

**Last Updated**: January 1, 2026
