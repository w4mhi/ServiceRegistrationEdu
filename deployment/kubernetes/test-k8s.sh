#!/bin/bash

# Kubernetes Testing Script
# Tests Service Registry deployment in K8s cluster

set -e

echo "=== Service Registry Kubernetes Testing Script ==="
echo "Current date: $(date)"
echo "Kubernetes cluster: $(kubectl config current-context)"
echo ""

# Configuration
NAMESPACE="service-registry-test"
IMAGE_TAG="${IMAGE_TAG:-test-$(date +%s)}"
REGISTRY="${REGISTRY:-localhost:5000}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Cleanup function
cleanup() {
    log_info "Cleaning up test resources..."
    kubectl delete namespace ${NAMESPACE} --ignore-not-found=true
}

# Register cleanup on exit
trap cleanup EXIT

# Step 1: Create namespace
log_info "Creating test namespace: ${NAMESPACE}"
kubectl create namespace ${NAMESPACE} || true
kubectl config set-context --current --namespace=${NAMESPACE}

# Step 2: Create secrets
log_info "Creating database credentials secret..."
kubectl create secret generic postgres-credentials \
  --from-literal=username=serviceregistry \
  --from-literal=password=test-password-$(date +%s) \
  --namespace=${NAMESPACE} \
  --dry-run=client -o yaml | kubectl apply -f -

# Step 3: Deploy PostgreSQL
log_info "Deploying PostgreSQL..."
cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ConfigMap
metadata:
  name: postgres-config
  namespace: ${NAMESPACE}
data:
  POSTGRES_DB: serviceregistry

---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-pvc
  namespace: ${NAMESPACE}
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 1Gi

---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  namespace: ${NAMESPACE}
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
  namespace: ${NAMESPACE}
spec:
  selector:
    app: postgres
  ports:
  - port: 5432
    targetPort: 5432
  clusterIP: None
EOF

# Step 4: Wait for PostgreSQL to be ready
log_info "Waiting for PostgreSQL to be ready..."
kubectl wait --for=condition=ready pod -l app=postgres --timeout=120s -n ${NAMESPACE}

# Step 5: Build and push API image (if not using existing)
if [[ "${SKIP_BUILD}" != "true" ]]; then
    log_info "Building API Docker image..."
    docker build -t ${REGISTRY}/service-registry-api:${IMAGE_TAG} \
        -f deployment/docker/Dockerfile .
    
    if [[ "${REGISTRY}" != "localhost:5000" ]]; then
        log_info "Pushing image to registry..."
        docker push ${REGISTRY}/service-registry-api:${IMAGE_TAG}
    fi
else
    log_warn "Skipping image build (SKIP_BUILD=true)"
fi

# Step 6: Create API secret
log_info "Creating API secrets..."
kubectl create secret generic api-secrets \
  --from-literal=ConnectionStrings__ServiceRegistry="Host=postgres-service;Database=serviceregistry;Username=serviceregistry;Password=test-password" \
  --namespace=${NAMESPACE} \
  --dry-run=client -o yaml | kubectl apply -f -

# Step 7: Deploy API
log_info "Deploying Service Registry API..."
cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ConfigMap
metadata:
  name: api-config
  namespace: ${NAMESPACE}
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  ASPNETCORE_HTTP_PORTS: "8080"
  DatabaseProvider: "Postgres"

---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: service-registry-api
  namespace: ${NAMESPACE}
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
        image: ${REGISTRY}/service-registry-api:${IMAGE_TAG}
        imagePullPolicy: IfNotPresent
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
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "200m"
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10

---
apiVersion: v1
kind: Service
metadata:
  name: service-registry-api
  namespace: ${NAMESPACE}
spec:
  type: ClusterIP
  selector:
    app: service-registry-api
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
    name: http
EOF

# Step 8: Wait for API to be ready
log_info "Waiting for API deployment to be ready..."
kubectl wait --for=condition=available deployment/service-registry-api --timeout=180s -n ${NAMESPACE}

# Step 9: Run functional tests
log_info "Running functional tests..."

# Port-forward to API
kubectl port-forward svc/service-registry-api 8080:80 -n ${NAMESPACE} &
PF_PID=$!
sleep 5

# Test health endpoint
log_info "Testing health endpoint..."
HEALTH_RESPONSE=$(curl -s http://localhost:8080/health)
if echo "${HEALTH_RESPONSE}" | grep -q "Healthy"; then
    log_info "✓ Health check passed"
else
    log_error "✗ Health check failed: ${HEALTH_RESPONSE}"
    kill $PF_PID 2>/dev/null || true
    exit 1
fi

# Test registration endpoint
log_info "Testing registration endpoint..."
REGISTER_RESPONSE=$(curl -s -X POST http://localhost:8080/api/v1/register \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "k8s-test-service",
    "description": "Kubernetes test service",
    "contactEmail": "k8s@test.com",
    "endpoints": ["http://localhost:8080"],
    "heartbeatTimeout": 30,
    "maxMissedHeartbeats": 5
  }')

if echo "${REGISTER_RESPONSE}" | grep -q "registrationId"; then
    log_info "✓ Registration endpoint passed"
    REGISTRATION_ID=$(echo "${REGISTER_RESPONSE}" | grep -o '"registrationId":"[^"]*' | cut -d'"' -f4)
    log_info "  Registration ID: ${REGISTRATION_ID}"
else
    log_error "✗ Registration endpoint failed: ${REGISTER_RESPONSE}"
    kill $PF_PID 2>/dev/null || true
    exit 1
fi

# Test status endpoint
log_info "Testing status endpoint..."
STATUS_RESPONSE=$(curl -s http://localhost:8080/api/v1/status/registration/${REGISTRATION_ID})
if echo "${STATUS_RESPONSE}" | grep -q "pending"; then
    log_info "✓ Status endpoint passed"
else
    log_error "✗ Status endpoint failed: ${STATUS_RESPONSE}"
    kill $PF_PID 2>/dev/null || true
    exit 1
fi

# Cleanup port-forward
kill $PF_PID 2>/dev/null || true

# Step 10: Check pod logs for errors
log_info "Checking API pod logs..."
API_POD=$(kubectl get pods -l app=service-registry-api -n ${NAMESPACE} -o jsonpath='{.items[0].metadata.name}')
ERROR_COUNT=$(kubectl logs ${API_POD} -n ${NAMESPACE} | grep -i error | wc -l | tr -d ' ')
if [ "$ERROR_COUNT" -eq "0" ]; then
    log_info "✓ No errors found in logs"
else
    log_warn "⚠ Found ${ERROR_COUNT} error(s) in logs"
    kubectl logs ${API_POD} -n ${NAMESPACE} | grep -i error | tail -n 10
fi

# Step 11: Test horizontal scaling
log_info "Testing horizontal pod autoscaling..."
kubectl scale deployment/service-registry-api --replicas=3 -n ${NAMESPACE}
kubectl wait --for=condition=available deployment/service-registry-api --timeout=60s -n ${NAMESPACE}
REPLICA_COUNT=$(kubectl get deployment service-registry-api -n ${NAMESPACE} -o jsonpath='{.status.availableReplicas}')
if [ "$REPLICA_COUNT" -eq "3" ]; then
    log_info "✓ Scaling test passed (${REPLICA_COUNT} replicas)"
else
    log_error "✗ Scaling test failed (expected 3, got ${REPLICA_COUNT})"
    exit 1
fi

# Summary
echo ""
log_info "=== Kubernetes Testing Summary ==="
log_info "✓ PostgreSQL deployment successful"
log_info "✓ API deployment successful"
log_info "✓ Health check passed"
log_info "✓ Registration flow passed"
log_info "✓ Status query passed"
log_info "✓ Horizontal scaling passed"
echo ""
log_info "All tests passed successfully!"
log_info "Namespace: ${NAMESPACE}"
log_info "Image: ${REGISTRY}/service-registry-api:${IMAGE_TAG}"
echo ""

# Keep namespace for manual inspection if requested
if [[ "${KEEP_NAMESPACE}" == "true" ]]; then
    log_info "Namespace ${NAMESPACE} preserved for manual inspection"
    log_info "To delete: kubectl delete namespace ${NAMESPACE}"
    trap - EXIT
else
    log_info "Cleaning up namespace in 10 seconds (set KEEP_NAMESPACE=true to preserve)..."
    sleep 10
fi
