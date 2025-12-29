# Service Registry Deployment Guide

This guide covers deployment options for the Service Registry system using Docker and Kubernetes.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Docker Deployment](#docker-deployment)
  - [Single Container](#single-container)
  - [Docker Compose with PostgreSQL](#docker-compose-with-postgresql)
  - [Docker Compose with Redis](#docker-compose-with-redis)
- [Kubernetes Deployment](#kubernetes-deployment)
  - [PostgreSQL Backend](#postgresql-backend)
  - [Redis Backend](#redis-backend)
  - [Horizontal Pod Autoscaling](#horizontal-pod-autoscaling)
- [Configuration](#configuration)
- [Health Checks](#health-checks)
- [Monitoring](#monitoring)
- [Troubleshooting](#troubleshooting)

## Prerequisites

### Docker Deployment
- Docker Engine 20.10+
- Docker Compose 2.0+
- 2GB RAM minimum
- 10GB disk space

### Kubernetes Deployment
- Kubernetes 1.28+
- kubectl configured
- Helm 3.0+ (optional)
- 4GB RAM minimum per node
- 20GB disk space
- Persistent Volume support (for databases)

## Docker Deployment

### Single Container

Build and run the API container with in-memory storage (testing only):

```bash
# Build the image
docker build -t service-registry-api:latest -f deployment/docker/Dockerfile .

# Run with in-memory storage
docker run -d \
  --name service-registry \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e DatabaseProvider=InMemory \
  service-registry-api:latest
```

Access the API at `http://localhost:8080` and Scalar documentation at `http://localhost:8080/scalar/v1`.

### Docker Compose with PostgreSQL

The recommended production deployment uses PostgreSQL for persistent storage.

**File: `deployment/docker/docker-compose.yml`**

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15-alpine
    container_name: service-registry-db
    environment:
      POSTGRES_DB: serviceregistry
      POSTGRES_USER: serviceregistry
      POSTGRES_PASSWORD: ${DB_PASSWORD:-Change_Me_In_Production}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U serviceregistry"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - service-registry-network

  api:
    build:
      context: ../..
      dockerfile: deployment/docker/Dockerfile
    container_name: service-registry-api
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: 8080
      DatabaseProvider: Postgres
      ConnectionStrings__ServiceRegistry: "Host=postgres;Database=serviceregistry;Username=serviceregistry;Password=${DB_PASSWORD:-Change_Me_In_Production}"
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    networks:
      - service-registry-network
    restart: unless-stopped

  dashboard:
    build:
      context: ../..
      dockerfile: deployment/docker/Dockerfile.dashboard
    container_name: service-registry-dashboard
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: 8081
      ApiBaseUrl: http://api:8080
    ports:
      - "8081:8081"
    depends_on:
      - api
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8081/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - service-registry-network
    restart: unless-stopped

volumes:
  postgres-data:
    driver: local

networks:
  service-registry-network:
    driver: bridge
```

**Deploy with PostgreSQL:**

```bash
cd deployment/docker

# Set database password (recommended)
export DB_PASSWORD="your-secure-password"

# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Check service health
docker-compose ps

# Stop services
docker-compose down

# Stop and remove volumes (CAUTION: deletes data)
docker-compose down -v
```

### Docker Compose with Redis

For deployments requiring ultra-low latency and high throughput:

```bash
cd deployment/docker

# Use Redis profile
docker-compose --profile redis up -d

# Or specify Redis compose file
docker-compose -f docker-compose.redis.yml up -d
```

**File: `deployment/docker/docker-compose.redis.yml`**

```yaml
version: '3.8'

services:
  redis:
    image: redis/redis-stack-server:latest
    container_name: service-registry-redis
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    command: >
      redis-stack-server 
      --save 60 1
      --appendonly yes
      --requirepass ${REDIS_PASSWORD:-Change_Me_In_Production}
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - service-registry-network

  api:
    build:
      context: ../..
      dockerfile: deployment/docker/Dockerfile
    container_name: service-registry-api
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: 8080
      DatabaseProvider: Redis
      ConnectionStrings__ServiceRegistry: "redis:6379,password=${REDIS_PASSWORD:-Change_Me_In_Production}"
    ports:
      - "8080:8080"
    depends_on:
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - service-registry-network
    restart: unless-stopped

volumes:
  redis-data:
    driver: local

networks:
  service-registry-network:
    driver: bridge
```

## Kubernetes Deployment

### PostgreSQL Backend

**Prerequisites:**
- StorageClass configured for persistent volumes
- kubectl context set to target cluster

**Step 1: Create Namespace**

```bash
kubectl create namespace service-registry
kubectl config set-context --current --namespace=service-registry
```

**Step 2: Create Secrets**

```bash
# Database credentials
kubectl create secret generic postgres-credentials \
  --from-literal=username=serviceregistry \
  --from-literal=password=your-secure-password \
  --namespace=service-registry

# Connection string
kubectl create secret generic api-secrets \
  --from-literal=ConnectionStrings__ServiceRegistry="Host=postgres-service;Database=serviceregistry;Username=serviceregistry;Password=your-secure-password" \
  --namespace=service-registry
```

**Step 3: Deploy PostgreSQL**

**File: `deployment/kubernetes/postgres.yaml`**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: postgres-config
  namespace: service-registry
data:
  POSTGRES_DB: serviceregistry

---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-pvc
  namespace: service-registry
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 10Gi
  storageClassName: standard # Update to your storage class

---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  namespace: service-registry
spec:
  serviceName: postgres-service
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
      - name: postgres
        image: postgres:15-alpine
        ports:
        - containerPort: 5432
          name: postgres
        env:
        - name: POSTGRES_DB
          valueFrom:
            configMapKeyRef:
              name: postgres-config
              key: POSTGRES_DB
        - name: POSTGRES_USER
          valueFrom:
            secretKeyRef:
              name: postgres-credentials
              key: username
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: postgres-credentials
              key: password
        volumeMounts:
        - name: postgres-storage
          mountPath: /var/lib/postgresql/data
        livenessProbe:
          exec:
            command:
            - pg_isready
            - -U
            - serviceregistry
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          exec:
            command:
            - pg_isready
            - -U
            - serviceregistry
          initialDelaySeconds: 5
          periodSeconds: 5
      volumes:
      - name: postgres-storage
        persistentVolumeClaim:
          claimName: postgres-pvc

---
apiVersion: v1
kind: Service
metadata:
  name: postgres-service
  namespace: service-registry
spec:
  selector:
    app: postgres
  ports:
  - port: 5432
    targetPort: 5432
  clusterIP: None # Headless service for StatefulSet
```

Apply:

```bash
kubectl apply -f deployment/kubernetes/postgres.yaml
```

**Step 4: Run Database Migrations**

**File: `deployment/kubernetes/migration-job.yaml`**

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: db-migration
  namespace: service-registry
spec:
  template:
    spec:
      containers:
      - name: migration
        image: your-registry/service-registry-api:latest # Update with your image
        command:
        - dotnet
        - ef
        - database
        - update
        - --connection
        - $(ConnectionStrings__ServiceRegistry)
        env:
        - name: ConnectionStrings__ServiceRegistry
          valueFrom:
            secretKeyRef:
              name: api-secrets
              key: ConnectionStrings__ServiceRegistry
      restartPolicy: OnFailure
  backoffLimit: 3
```

Apply:

```bash
kubectl apply -f deployment/kubernetes/migration-job.yaml
kubectl wait --for=condition=complete --timeout=300s job/db-migration -n service-registry
```

**Step 5: Deploy API**

**File: `deployment/kubernetes/api-deployment.yaml`**

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: api-config
  namespace: service-registry
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  ASPNETCORE_HTTP_PORTS: "8080"
  DatabaseProvider: "Postgres"

---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: service-registry-api
  namespace: service-registry
spec:
  replicas: 2
  selector:
    matchLabels:
      app: service-registry-api
  template:
    metadata:
      labels:
        app: service-registry-api
    spec:
      containers:
      - name: api
        image: your-registry/service-registry-api:latest # Update
        ports:
        - containerPort: 8080
          name: http
        envFrom:
        - configMapRef:
            name: api-config
        env:
        - name: ConnectionStrings__ServiceRegistry
          valueFrom:
            secretKeyRef:
              name: api-secrets
              key: ConnectionStrings__ServiceRegistry
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5

---
apiVersion: v1
kind: Service
metadata:
  name: service-registry-api
  namespace: service-registry
spec:
  type: LoadBalancer # Or ClusterIP with Ingress
  selector:
    app: service-registry-api
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
    name: http
```

Apply:

```bash
kubectl apply -f deployment/kubernetes/api-deployment.yaml
```

### Horizontal Pod Autoscaling

**File: `deployment/kubernetes/hpa.yaml`**

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: service-registry-api-hpa
  namespace: service-registry
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: service-registry-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Pods
        value: 1
        periodSeconds: 60
```

Apply:

```bash
kubectl apply -f deployment/kubernetes/hpa.yaml
```

### Redis Backend

For Redis deployment in Kubernetes, use Redis Operator or Helm chart:

```bash
# Using Helm
helm repo add bitnami https://charts.bitnami.com/bitnami
helm install redis bitnami/redis \
  --namespace service-registry \
  --set auth.password=your-secure-password \
  --set master.persistence.enabled=true \
  --set master.persistence.size=10Gi

# Update API deployment with Redis connection
kubectl set env deployment/service-registry-api \
  DatabaseProvider=Redis \
  ConnectionStrings__ServiceRegistry="redis-master:6379,password=your-secure-password"
```

## Configuration

### Environment Variables

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | Development | No |
| `ASPNETCORE_HTTP_PORTS` | HTTP port | 8080 | No |
| `DatabaseProvider` | Storage backend (Postgres/Redis/InMemory) | Postgres | Yes |
| `ConnectionStrings__ServiceRegistry` | Database connection string | - | Yes |

### appsettings.Production.json

Override settings for production:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Omni.ServiceRegistry": "Information"
    }
  },
  "AllowedHosts": "*",
  "DatabaseProvider": "Postgres"
}
```

## Health Checks

### API Health Endpoint

```bash
curl http://localhost:8080/health
```

Expected response:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456"
}
```

### Kubernetes Liveness/Readiness

Configured in deployment manifests:
- **Liveness**: `/health` every 10s
- **Readiness**: `/health` every 5s

## Monitoring

### Logs

**Docker:**
```bash
docker logs -f service-registry-api
```

**Kubernetes:**
```bash
kubectl logs -f deployment/service-registry-api -n service-registry
```

### Metrics

Performance metrics are logged for all API endpoints:
- Registration: Target <2000ms
- Heartbeat: Target <500ms
- Status queries: Target <1000ms

Search logs for performance warnings:
```bash
kubectl logs deployment/service-registry-api -n service-registry | grep "performance degraded"
```

## Troubleshooting

### API Won't Start

**Check database connection:**
```bash
# Docker
docker exec service-registry-api dotnet --version

# Kubernetes
kubectl exec -it deployment/service-registry-api -n service-registry -- /bin/bash
```

**Verify environment variables:**
```bash
# Docker
docker exec service-registry-api env | grep Database

# Kubernetes
kubectl exec deployment/service-registry-api -n service-registry -- env | grep Database
```

### Database Migration Issues

**Re-run migrations:**
```bash
# Docker
docker exec service-registry-api dotnet ef database update

# Kubernetes
kubectl delete job db-migration -n service-registry
kubectl apply -f deployment/kubernetes/migration-job.yaml
```

### Connection Refused Errors

**Check service connectivity:**
```bash
# Kubernetes - DNS resolution
kubectl run -it --rm debug --image=busybox --restart=Never -- nslookup postgres-service
kubectl run -it --rm debug --image=busybox --restart=Never -- nslookup service-registry-api
```

### Performance Issues

**Check pod resources:**
```bash
kubectl top pods -n service-registry
```

**Scale replicas:**
```bash
kubectl scale deployment/service-registry-api --replicas=5 -n service-registry
```

### SignalR Connection Failures

**Verify CORS configuration** in `Program.cs`:
```csharp
.WithOrigins("http://localhost:8081", "https://yourdomain.com")
.SetIsOriginAllowed(_ => true) // Required for SignalR
```

**Check Dashboard → API connectivity:**
```bash
# Docker
docker exec service-registry-dashboard curl http://api:8080/health

# Kubernetes
kubectl exec deployment/service-registry-dashboard -n service-registry -- curl http://service-registry-api/health
```

## Production Checklist

- [ ] Use strong database passwords (min 16 characters)
- [ ] Configure HTTPS/TLS certificates
- [ ] Set up persistent volumes for databases
- [ ] Configure resource limits and requests
- [ ] Enable horizontal pod autoscaling
- [ ] Set up monitoring and alerting
- [ ] Configure log aggregation
- [ ] Review and restrict CORS origins
- [ ] Enable authentication/authorization
- [ ] Set up regular database backups
- [ ] Configure health check thresholds
- [ ] Review security headers configuration
- [ ] Disable Scalar API docs in production (optional)
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`

## Additional Resources

- [ASP.NET Core Deployment](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/)
- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
- [Kubernetes Production Patterns](https://kubernetes.io/docs/concepts/cluster-administration/manage-deployment/)
- [PostgreSQL Docker Hub](https://hub.docker.com/_/postgres)
- [Redis Stack Docker Hub](https://hub.docker.com/r/redis/redis-stack-server)

---

**Last Updated**: November 16, 2025  
**Version**: 1.0.0
