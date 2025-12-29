# Kubernetes Deployment Guide

This directory contains Kubernetes manifests for deploying the Service Registry system to a Kubernetes cluster.

## Prerequisites

- Kubernetes cluster (1.25+)
- kubectl configured to access your cluster
- Docker images built and available:
  - `service-registry-api:latest`
  - `service-registry-dashboard:latest`
- NGINX Ingress Controller (optional, for ingress)
- cert-manager (optional, for TLS certificates)

## Architecture

The deployment consists of:

- **PostgreSQL StatefulSet**: Persistent database storage with 10Gi PVC
- **Redis StatefulSet**: Cache and session storage with 5Gi PVC
- **API Deployment**: 2-10 replicas with HPA (CPU-based autoscaling)
- **Dashboard Deployment**: 2-5 replicas with HPA
- **Migration Job**: Runs EF Core migrations before deployment
- **Services**: ClusterIP services for internal communication
- **Ingress**: External access via nginx-ingress (optional)

## Quick Start

### 1. Build Docker Images

```bash
# From repository root
cd deployment/docker

# Build API image
docker build -f Dockerfile.api -t service-registry-api:latest ../..

# Build Dashboard image
docker build -f Dockerfile.dashboard -t service-registry-dashboard:latest ../..
```

### 2. Update Secrets

**IMPORTANT**: Change the default passwords in `02-secret.yaml`:

```yaml
stringData:
  postgres-connection: "Host=postgres-service;Port=5432;Database=serviceregistry;Username=postgres;Password=YOUR_SECURE_PASSWORD"
```

### 3. Deploy to Kubernetes

```bash
# Apply all manifests in order
kubectl apply -f 00-namespace.yaml
kubectl apply -f 01-configmap.yaml
kubectl apply -f 02-secret.yaml
kubectl apply -f 03-postgres.yaml
kubectl apply -f 04-redis.yaml

# Wait for databases to be ready
kubectl wait --for=condition=ready pod -l app=postgres -n service-registry --timeout=120s
kubectl wait --for=condition=ready pod -l app=redis -n service-registry --timeout=120s

# Run database migrations
kubectl apply -f 05-migration-job.yaml
kubectl wait --for=condition=complete job/db-migration -n service-registry --timeout=300s

# Deploy applications
kubectl apply -f 06-api-deployment.yaml
kubectl apply -f 07-dashboard-deployment.yaml
kubectl apply -f 08-hpa.yaml

# (Optional) Setup ingress
kubectl apply -f 09-ingress.yaml
```

### 4. Verify Deployment

```bash
# Check all pods are running
kubectl get pods -n service-registry

# Check services
kubectl get svc -n service-registry

# Check HPA status
kubectl get hpa -n service-registry

# View API logs
kubectl logs -l app=service-registry-api -n service-registry --tail=50

# View Dashboard logs
kubectl logs -l app=service-registry-dashboard -n service-registry --tail=50
```

## Access the Services

### Port Forwarding (Development)

```bash
# Forward API
kubectl port-forward -n service-registry svc/api-service 8080:80

# Forward Dashboard (in another terminal)
kubectl port-forward -n service-registry svc/dashboard-service 8081:80

# Access:
# API: http://localhost:8080
# Dashboard: http://localhost:8081
```

### Ingress (Production)

If you deployed the ingress:

1. Add DNS entries pointing to your ingress controller:
   - `api.service-registry.local` → Ingress IP
   - `dashboard.service-registry.local` → Ingress IP

2. Access:
   - API: https://api.service-registry.local
   - Dashboard: https://dashboard.service-registry.local

## Configuration

### ConfigMap (`01-configmap.yaml`)

Environment-specific settings:
- Database provider selection
- Heartbeat monitoring intervals
- Rate limiting thresholds
- CORS allowed origins
- Logging levels

### Secrets (`02-secret.yaml`)

Sensitive configuration:
- PostgreSQL connection string
- Redis connection string

**Always update secrets before production deployment!**

## Resource Limits

### API Pods
- Requests: 250m CPU, 256Mi memory
- Limits: 1 CPU, 1Gi memory
- HPA: 2-10 replicas, target 70% CPU

### Dashboard Pods
- Requests: 250m CPU, 256Mi memory
- Limits: 1 CPU, 1Gi memory
- HPA: 2-5 replicas, target 70% CPU

### PostgreSQL
- Requests: 250m CPU, 256Mi memory
- Limits: 1 CPU, 1Gi memory
- Storage: 10Gi PVC

### Redis
- Requests: 100m CPU, 128Mi memory
- Limits: 500m CPU, 512Mi memory
- Storage: 5Gi PVC

## Health Checks

All deployments include:
- **Liveness Probe**: Detects and restarts unhealthy containers
- **Readiness Probe**: Ensures traffic only goes to ready pods

API & Dashboard health endpoint: `GET /health`

## Scaling

### Manual Scaling

```bash
# Scale API
kubectl scale deployment service-registry-api -n service-registry --replicas=5

# Scale Dashboard
kubectl scale deployment service-registry-dashboard -n service-registry --replicas=3
```

### Horizontal Pod Autoscaling (HPA)

HPA is configured to automatically scale based on:
- CPU utilization (target: 70%)
- Memory utilization (target: 80%)

Monitor HPA:
```bash
kubectl get hpa -n service-registry -w
```

## Database Migrations

### Running Migrations

Migrations run automatically via the migration job. To manually trigger:

```bash
# Delete old job
kubectl delete job db-migration -n service-registry

# Re-apply
kubectl apply -f 05-migration-job.yaml

# Monitor progress
kubectl logs -f job/db-migration -n service-registry
```

### Creating New Migrations

On your development machine:

```bash
cd /path/to/repo
dotnet ef migrations add YourMigrationName \
  --project src/Omni.ServiceRegistry.Data/Omni.ServiceRegistry.Data.csproj \
  --startup-project src/Omni.ServiceRegistry.Api/Omni.ServiceRegistry.Api.csproj
```

Rebuild Docker images and redeploy.

## Monitoring

### View Logs

```bash
# API logs
kubectl logs -l app=service-registry-api -n service-registry --tail=100 -f

# Dashboard logs
kubectl logs -l app=service-registry-dashboard -n service-registry --tail=100 -f

# PostgreSQL logs
kubectl logs -l app=postgres -n service-registry --tail=100 -f
```

### Events

```bash
kubectl get events -n service-registry --sort-by='.lastTimestamp'
```

### Resource Usage

```bash
kubectl top pods -n service-registry
kubectl top nodes
```

## Troubleshooting

### Pods Not Starting

```bash
# Describe pod for events
kubectl describe pod <pod-name> -n service-registry

# Check logs
kubectl logs <pod-name> -n service-registry

# Check if images are available
kubectl get pods -n service-registry -o jsonpath='{.items[*].status.containerStatuses[*].imageID}'
```

### Database Connection Issues

```bash
# Check PostgreSQL is ready
kubectl exec -it postgres-0 -n service-registry -- psql -U postgres -d serviceregistry -c "\l"

# Test connection from API pod
kubectl exec -it <api-pod-name> -n service-registry -- sh
# Then: nc -zv postgres-service 5432
```

### Migration Job Failed

```bash
# View job logs
kubectl logs job/db-migration -n service-registry

# Delete and retry
kubectl delete job db-migration -n service-registry
kubectl apply -f 05-migration-job.yaml
```

## Cleanup

To remove the entire deployment:

```bash
# Delete all resources
kubectl delete -f 09-ingress.yaml
kubectl delete -f 08-hpa.yaml
kubectl delete -f 07-dashboard-deployment.yaml
kubectl delete -f 06-api-deployment.yaml
kubectl delete -f 05-migration-job.yaml
kubectl delete -f 04-redis.yaml
kubectl delete -f 03-postgres.yaml
kubectl delete -f 02-secret.yaml
kubectl delete -f 01-configmap.yaml

# Delete namespace (removes everything)
kubectl delete namespace service-registry

# Note: PVCs may need manual deletion
kubectl delete pvc -n service-registry --all
```

## Production Considerations

1. **Secrets Management**: Use external secret management (e.g., Sealed Secrets, External Secrets Operator, Vault)
2. **Monitoring**: Integrate with Prometheus/Grafana for metrics
3. **Logging**: Set up log aggregation (ELK, Loki, etc.)
4. **Backups**: Implement PostgreSQL backup strategy
5. **TLS**: Configure cert-manager with Let's Encrypt for automatic TLS
6. **Network Policies**: Restrict pod-to-pod communication
7. **Resource Quotas**: Set namespace resource quotas
8. **Pod Disruption Budgets**: Ensure availability during updates
9. **Image Scanning**: Scan images for vulnerabilities before deployment
10. **GitOps**: Use ArgoCD or Flux for declarative deployments

## Performance Tuning

### PostgreSQL

```bash
# Connect to postgres pod
kubectl exec -it postgres-0 -n service-registry -- psql -U postgres -d serviceregistry

# Analyze query performance
EXPLAIN ANALYZE SELECT * FROM "Services" WHERE "HealthStatus" = 0;

# Check indexes
\di
```

### API Rate Limiting

Adjust in ConfigMap:
```yaml
RateLimiting__HeartbeatPermitLimit: "500"  # Increase for higher throughput
```

Redeploy:
```bash
kubectl rollout restart deployment/service-registry-api -n service-registry
```

## Support

For issues or questions:
1. Check pod logs and events
2. Review configuration in ConfigMap and Secrets
3. Verify database connectivity
4. Check resource usage and HPA status
5. Refer to main documentation in repository root
