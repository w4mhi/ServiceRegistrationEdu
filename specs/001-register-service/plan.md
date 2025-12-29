# Implementation Plan: Service Registration

**Branch**: `1-register-service` | **Date**: November 9, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/1-register-service/spec.md`

## Summary

This feature implements a REST API-based service registration system with heartbeat monitoring. Services automatically register themselves via API endpoints, undergo administrator approval, and maintain health status through periodic heartbeat signals. The system tracks six health states (HEALTHY, UNHEALTHY, DEGRADED, DEAD, RECOVERED, DELETED) and provides comprehensive service catalog and discovery capabilities.

## Technical Context

**Language/Version**: C# / .NET 8.0  
**Primary Dependencies**: ASP.NET Core 8.0, Blazor Server, SignalR, StackExchange.Redis (RedisJSON), ILogger  
**Storage**: Pluggable backend via repository abstraction
- **Options**: PostgreSQL (EF Core 8.0), Redis (RedisJSON module), SQL Server (EF Core), File (testing)
- **Selection**: Configured via appsettings.json `DatabaseProvider` property
- **Active**: One backend per deployment (operator choice)
**Testing**: xUnit, Moq, FluentAssertions, bUnit (Blazor component testing)  
**Target Platform**: Linux/Windows server, containerized deployment (Docker + Kubernetes)  
**Project Type**: Web application (API + Blazor dashboard) with pluggable storage  
**Performance Goals**: 
- Registration API: <2s response time
- Status query API: <1s response time
- Heartbeat API: <500ms response time
- Heartbeat processing: ≥10,000 heartbeats/minute (optimized: write only on status change or periodic snapshot)

**Constraints**: 
- 99.5% uptime during business hours
- Support ≥1,000 registered services
- Support ≥50 concurrent users
- Health status update latency: ≤1 timeout period
- Storage backend abstraction (no direct SQL queries in business logic)

**Scale/Scope**: 
- Initial: 1,000 services
- 10,000 heartbeats/minute sustained throughput
- 8 REST API endpoints
- Blazor dashboard with 3 main pages (monitoring, pending approvals, service detail)
- 61 functional requirements (R1-R61)
- Docker + Kubernetes deployment support (Postgres + Redis options)
- 14 research topics addressing storage abstraction, optimized writes, Redis integration

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The following MUST be explicitly addressed before proceeding:

- **Registry Integration**: Service registration data includes serviceId (UUID), serviceName, normalizedServiceName (hash), description, owner, contactEmail, endpoints[], heartbeatTimeout, maxMissedHeartbeats. ✅
- **Discovery Read Model**: Service catalog searchable by name, owner, environment, keywords; status query API P95 ≤1s per requirement. ✅
- **Heartbeat Strategy**: Configurable interval per service, timeout period, maxMissedHeartbeats threshold (R1, R33); optimized writes (status change + periodic snapshots only). ✅
- **Health Status Transitions**: HEALTHY→UNHEALTHY (missed>0), UNHEALTHY→DEGRADED (missed≥50% max), DEGRADED→DEAD (missed>max), DEAD→RECOVERED→HEALTHY defined (R36-R46). ✅
- **Re-Registration Flow**: Soft-delete with DELETED status; re-registration requires new service ID; idempotency via normalized name hash (R3-R5, R30-R31). ✅
- **Observability**: ILogger with trace/info/warning/error levels; log registration requests, approval actions, status transitions, heartbeat failures (R62-R66). ✅
- **ACID Transactions**: Registration submission, approval/denial, status updates, heartbeat processing require atomicity (storage-provider-specific implementation). ✅
- **Storage Abstraction**: IStorageProvider (low-level) + IServiceRepository (domain-level) for pluggable backends (Postgres, Redis, SQL Server, File); no SQL in business logic. ✅
- **Namespace Plan**: Follow Omni.(Component).{Models|Interfaces|Services|Data} pattern; e.g., Omni.ServiceRegistry.Models, Omni.ServiceRegistry.Interfaces, Omni.ServiceRegistry.Data.Postgres, Omni.ServiceRegistry.Data.Redis. ✅
- **Directory.Packages.props**: All dependencies (ASP.NET Core 8.0, EF Core 8.0, Blazor, StackExchange.Redis, xUnit, bUnit) listed with central version management. ✅
- **Security**: Authentication/authorization deferred to P2 (documented in Open Issues); assumes existing auth infrastructure; HTTPS enforcement for production. ⚠️ (P2)
- **API Validation**: All endpoints validate inputs per R6; return HTTP 400/401/403/404/409/503 appropriately (Edge Cases section). ✅
- **Dashboard Requirements**: Blazor-based dashboard with monitoring page (R53), pending approvals page (R54), service detail page (R55), access control (R56); shares repository layer with API. ✅
- **Deployment Support**: Docker container (R57), Docker Compose with profiles (postgres/redis/sqlserver) (R58), Kubernetes manifests with StatefulSets (R59), EF Core migrations (SQL only) (R60), horizontal scaling (R61). ✅
- **Test Matrix**: Unit tests for services/validation, integration tests for API contracts/DB transactions, scenario-based tests from 13 user scenarios, Blazor component tests with bUnit, repository implementation tests (Postgres, Redis, File). ✅

Gate passes: 14/15 items ✅, 1 item ⚠️ (Security deferred to P2 with explicit documentation and mitigation assumption).

## Project Structure

### Documentation (this feature)

```text
specs/1-register-service/
├── spec.md              # Feature specification (completed)
├── plan.md              # This file
├── research.md          # Phase 0 output (to be created)
├── data-model.md        # Phase 1 output (to be created)
├── quickstart.md        # Phase 1 output (to be created)
├── contracts/           # Phase 1 output (to be created)
│   ├── registration-api.yaml
│   ├── status-api.yaml
│   ├── heartbeat-api.yaml
│   └── admin-api.yaml
└── checklists/
    └── requirements.md  # Quality checklist (completed)
```

### Source Code (repository root)

```text
src/
├── Omni.ServiceRegistry.Models/
│   ├── RegistrationRequest.cs
│   ├── Service.cs
│   ├── ServiceChangeHistory.cs
│   ├── HealthStatus.cs (enum)
│   ├── RegistrationStatus.cs (enum)
│   └── DeletionStatus.cs (enum)
├── Omni.ServiceRegistry.Interfaces/
│   ├── IRegistrationService.cs
│   ├── IServiceCatalogService.cs
│   ├── IHeartbeatService.cs
│   ├── IAdministratorService.cs
│   └── Repositories/
│       ├── IRegistrationRepository.cs
│       ├── IServiceRepository.cs
│       └── IChangeHistoryRepository.cs
├── Omni.ServiceRegistry.Services/
│   ├── RegistrationService.cs
│   ├── ServiceCatalogService.cs
│   ├── HeartbeatService.cs
│   ├── AdministratorService.cs
│   ├── ValidationService.cs
│   └── ServiceNameNormalizer.cs
├── Omni.ServiceRegistry.Data/
│   ├── IStorageProvider.cs (low-level abstraction)
│   └── StorageProviderFactory.cs (creates provider based on config)
├── Omni.ServiceRegistry.Data.Postgres/
│   ├── PostgresStorageProvider.cs
│   ├── ServiceRegistryDbContext.cs
│   ├── Repositories/
│   │   ├── PostgresServiceRepository.cs
│   │   ├── PostgresRegistrationRepository.cs
│   │   └── PostgresChangeHistoryRepository.cs
│   └── Migrations/
│       └── 20251109_InitialSchema.cs
├── Omni.ServiceRegistry.Data.Redis/
│   ├── RedisStorageProvider.cs
│   └── Repositories/
│       ├── RedisServiceRepository.cs
│       ├── RedisRegistrationRepository.cs
│       └── RedisChangeHistoryRepository.cs
├── Omni.ServiceRegistry.Data.File/
│   ├── FileStorageProvider.cs
│   └── Repositories/
│       ├── FileServiceRepository.cs
│       ├── FileRegistrationRepository.cs
│       └── FileChangeHistoryRepository.cs
├── Omni.ServiceRegistry.Api/
│   ├── Controllers/
│   │   ├── RegistrationController.cs
│   │   ├── StatusController.cs
│   │   ├── HeartbeatController.cs
│   │   └── AdminController.cs
│   ├── Middleware/
│   │   └── ExceptionHandlingMiddleware.cs
│   ├── DTOs/
│   │   ├── RegistrationRequestDto.cs
│   │   ├── RegistrationResponseDto.cs
│   │   ├── StatusResponseDto.cs
│   │   ├── HeartbeatRequestDto.cs
│   │   └── AdminResponseDto.cs
│   ├── Program.cs
│   └── appsettings.json (includes DatabaseProvider: "Postgres"|"Redis"|"SqlServer"|"File")
└── Omni.ServiceRegistry.Dashboard/
    ├── Pages/
    │   ├── Index.razor (monitoring dashboard)
    │   ├── PendingApprovals.razor
    │   ├── ServiceDetail.razor
    │   └── _Host.cshtml
    ├── Components/
    │   ├── ServiceStatusCard.razor
    │   ├── HealthIndicator.razor
    │   ├── ApprovalModal.razor
    │   └── ServiceTable.razor
    ├── Services/
    │   ├── DashboardDataService.cs
    │   └── SignalRHubClient.cs (for real-time updates)
    ├── Program.cs
    ├── _Imports.razor
    └── appsettings.json

deployment/
├── docker/
│   ├── Dockerfile
│   ├── docker-compose.yml (profiles: postgres, redis, sqlserver)
│   └── .dockerignore
└── kubernetes/
    ├── namespace.yaml
    ├── configmap.yaml
    ├── secret.yaml
    ├── postgres.yaml (StatefulSet + Service)
    ├── redis.yaml (StatefulSet + Service) [NEW]
    ├── migration-job.yaml (for SQL databases only)
    ├── api-deployment.yaml
    ├── dashboard-deployment.yaml
    └── hpa.yaml

tests/
├── Omni.ServiceRegistry.Services.Tests/
│   ├── Unit/
│   │   ├── RegistrationServiceTests.cs
│   │   ├── HeartbeatServiceTests.cs
│   │   ├── ValidationServiceTests.cs
│   │   └── ServiceNameNormalizerTests.cs
│   └── Integration/
│       ├── RegistrationFlowTests.cs
│       └── HeartbeatMonitoringTests.cs
├── Omni.ServiceRegistry.Data.Tests/
│   ├── Postgres/
│   │   ├── PostgresServiceRepositoryTests.cs
│   │   ├── PostgresRegistrationRepositoryTests.cs
│   │   └── ConcurrencyTests.cs
│   ├── Redis/
│   │   ├── RedisServiceRepositoryTests.cs
│   │   ├── RedisRegistrationRepositoryTests.cs
│   │   └── PersistenceTests.cs
│   └── File/
│       ├── FileServiceRepositoryTests.cs
│       └── FileRegistrationRepositoryTests.cs
├── Omni.ServiceRegistry.Api.Tests/
│   ├── Controllers/
│   │   ├── RegistrationControllerTests.cs
│   │   ├── StatusControllerTests.cs
│   │   ├── HeartbeatControllerTests.cs
│   │   └── AdminControllerTests.cs
│   └── Integration/
│       ├── RegistrationApiTests.cs
│       ├── HeartbeatApiTests.cs
│       └── AdminWorkflowTests.cs
├── Omni.ServiceRegistry.Dashboard.Tests/
│   ├── Components/
│   │   ├── ServiceStatusCardTests.cs
│   │   ├── HealthIndicatorTests.cs
│   │   └── ServiceTableTests.cs
│   └── Pages/
│       ├── IndexTests.cs
│       ├── PendingApprovalsTests.cs
│       └── ServiceDetailTests.cs
└── Omni.ServiceRegistry.Tests.Common/
    ├── Fixtures/
    ├── Builders/
    └── TestData/
```

**Structure Decision**: Web application structure combining API backend with Blazor Server dashboard. The project follows clean architecture with separate assemblies for Models, Interfaces, Services, and **multiple Data layer implementations** (Data.Postgres, Data.Redis, Data.File) accessed via IStorageProvider abstraction. API and Dashboard share the same service and repository layers. Deployment artifacts organized in `/deployment` directory with Docker Compose profiles and Kubernetes StatefulSets for both Postgres and Redis options.

## Complexity Tracking

No constitution violations requiring justification. All design decisions align with stated principles:
- Strong typing enforced (C# language)
- SOLID principles applied through interface segregation and dependency injection
- ILogger used non-nullable for application services per rule #6
- Single Responsibility followed with dedicated services for registration, catalog, heartbeat, and administration
- One class per file per rule #19
- Model/Interface/Library folder convention followed per rule #20

## Phase 0: Research & Decisions

### Research Topics

1. **Storage Backend Abstraction & Repository Pattern**
   - Topic: Design pluggable storage with IStorageProvider + IServiceRepository
   - Questions: Interface granularity, Postgres/Redis/File implementations, DI registration strategy

2. **Optimized Heartbeat Write Strategy**
   - Topic: Minimize database writes (write only on status change or periodic snapshot)
   - Questions: In-memory state management, write conditions, 95%+ write reduction validation

3. **Service Name Normalization & Hashing Strategy**
   - Topic: Implement lowercase hash for idempotent registration (R3-R5)
   - Questions: Hash algorithm selection (MD5, SHA256), collision handling

4. **Background Service for Heartbeat Timeout Detection**
   - Topic: Implement timer-based health status monitoring
   - Questions: IHostedService vs BackgroundService, timer intervals, cancellation tokens

5. **API Rate Limiting & Throttling**
   - Topic: Protect heartbeat endpoint from abuse
   - Questions: ASP.NET Core rate limiting middleware, per-service vs global limits

6. **Soft-Delete Pattern with Storage Backend Abstraction**
   - Topic: Implement DELETED status across different storage providers (R30)
   - Questions: SQL query filters vs application-layer filtering, unique constraints with soft delete

7. **Concurrent Registration Handling**
   - Topic: Prevent duplicate pending registrations across storage backends (R5)
   - Questions: SQL unique constraints, Redis Lua scripts, File locking, optimistic concurrency

9. **Blazor Server vs Blazor WebAssembly for Dashboard**
   - Topic: Choose Blazor hosting model for dashboard (R52-R56)
   - Questions: Real-time updates with SignalR, server load, authentication flow

10. **Real-time Dashboard Updates**
    - Topic: Reflect health status changes in dashboard within 10 seconds
    - Questions: SignalR hub implementation, push vs poll, connection management

11. **Docker Multi-Stage Build Optimization**
    - Topic: Create optimized container image (R57)
    - Questions: Build stage separation, runtime dependencies, image size reduction

12. **Kubernetes Horizontal Pod Autoscaling**
    - Topic: Auto-scale API pods based on load (R61)
    - Questions: HPA metrics (CPU, custom), min/max replicas, scaling thresholds

13. **Database Migrations in Kubernetes**
    - Topic: Apply EF Core migrations during deployment (R60 - SQL databases only)
    - Questions: Init containers vs job, idempotency, rollback strategy, Redis (no migrations)

14. **Redis as Storage Backend with JSON Module**
    - Topic: Implement RedisJSON-based storage provider as alternative to SQL
    - Questions: RedisJSON vs Redis Hashes vs Strings, persistence (RDB/AOF), indexing with RediSearch, StatefulSet deployment

### Output Artifact

**File**: `research.md` containing:
- Decision summary for each topic (14 topics)
- Rationale and alternatives considered
- Code patterns and library selections
- Storage provider implementations (Postgres, Redis, File)
- Performance implications (optimized write strategy, pluggable backends)
- References to official documentation

## Phase 1: Design & Contracts

### Prerequisites
- `research.md` complete with all decisions documented
- All "NEEDS CLARIFICATION" items resolved

### Design Artifacts

#### 1. Data Model (`data-model.md`)

Extract entities from spec and research decisions:

**Entities**:
- `RegistrationRequest`: registrationId (PK), serviceName, serviceNameNormalized, description, owner, contactEmail, endpoints (JSON), heartbeatTimeout, maxMissedHeartbeats, status (enum), submittedDate, approvedDeniedDate, approvedDeniedBy, comments
- `Service`: serviceId (PK), serviceName, serviceNameNormalized, description, owner, contactEmail, endpoints (JSON), heartbeatTimeout, maxMissedHeartbeats, healthStatus (enum), deletionStatus (enum), deletionRequestedDate, deletionRequestedBy, deletionApprovedDate, deletionApprovedBy, missedHeartbeatCounter, lastHeartbeatTimestamp, lastHeartbeatClientStatus, registrationDate, lastUpdatedDate, registeredBy
- `ServiceChangeHistory`: changeId (PK), serviceId (FK), changedFields, previousValue, newValue, changedBy, changeTimestamp

**Relationships**:
- RegistrationRequest → Service (1:1 after approval)
- Service → ServiceChangeHistory (1:N)

**State Machines**:
- RegistrationStatus: pending → approved | denied
- HealthStatus: HEALTHY ⟷ UNHEALTHY ⟷ DEGRADED ⟷ DEAD → RECOVERED → HEALTHY
- DeletionStatus: active → pending_deletion → deleted

**Validation Rules**: Per R6 - name format, email format, URL format, positive integers for timeout/max missed

**Indexes**: 
- serviceNameNormalized (unique where status != deleted)
- healthStatus (for catalog queries)
- lastHeartbeatTimestamp (for timeout detection)
- registrationStatus (for pending admin queue)

#### 2. API Contracts (`/contracts/`)

Generate OpenAPI 3.0 specifications:

**`registration-api.yaml`**:
- POST `/api/v1/register` - Submit registration
- GET `/api/v1/status/registration/{id}` - Query registration status

**`status-api.yaml`**:
- GET `/api/v1/status/service/{id}` - Query service status and details

**`heartbeat-api.yaml`**:
- POST `/api/v1/heartbeat/{serviceId}` - Submit heartbeat

**`admin-api.yaml`**:
- GET `/api/v1/admin/registrations/pending` - List pending registrations
- POST `/api/v1/admin/registrations/{id}/approve` - Approve registration
- POST `/api/v1/admin/registrations/{id}/deny` - Deny registration
- POST `/api/v1/delete/{id}` - Request service deletion

Each contract includes:
- Request/response schemas with validation rules
- HTTP status codes (200, 400, 401, 403, 404, 409, 503)
- Error response format (consistent across all endpoints)
- Authentication requirements (marked as "assumed from P2")

#### 3. Dashboard Design (`/dashboard/`)

**NOTE**: Dashboard artifacts are documented here for reference, but actual Blazor components will be implemented in Phase 2.

**Pages** (Requirements R53-R56):
- `Index.razor` (Monitoring Dashboard):
  - Service count by health status (HEALTHY, UNHEALTHY, DEGRADED, DEAD)
  - Filterable table with service name, health status, last heartbeat, owner
  - Visual health indicators (green/yellow/orange/red)
  - Auto-refresh with SignalR push notifications
  
- `PendingApprovals.razor` (Administrator Approval Queue):
  - Pending registrations table with service name, owner, submitted date, endpoints
  - Pending deletions table with service name, owner, requested date
  - Approve/deny modal dialogs with comment fields
  - Real-time updates when new requests arrive
  
- `ServiceDetail.razor` (Service Details View):
  - Full service metadata display
  - Health status history chart
  - Heartbeat timeline (last 24 hours)
  - Change audit log table

**Components**:
- `ServiceStatusCard.razor`: Reusable service display with health indicator
- `ApprovalModal.razor`: Approval/denial dialog with comments
- `HealthStatusBadge.razor`: Color-coded health status badge

**Services**:
- `IServiceCatalogService`: Interface for fetching service data
- `ServiceMonitorHub`: SignalR hub for real-time dashboard updates
  - `HealthStatusChanged(serviceId, newStatus)`: Push health status changes
  - `NewRegistrationRequest(registrationId)`: Notify admins of pending requests

**Real-time Updates** (Research Topic 9):
- SignalR server-to-client push notifications
- Dashboard subscribes to ServiceMonitorHub on component initialization
- Health status changes pushed within 10 seconds (Requirement R13)
- Client uses `InvokeAsync(StateHasChanged)` to trigger re-render

**Testing** (bUnit):
- Component rendering tests
- SignalR mock subscription tests
- User interaction tests (approve/deny actions)

#### 4. Deployment Artifacts (`/deployment/`)

**Docker** (`/deployment/docker/`):
- `Dockerfile`: Multi-stage build (Research Topic 10)
  - Stage 1 (build): dotnet/sdk:8.0, compile all projects
  - Stage 2 (api-runtime): dotnet/aspnet:8.0, API only (~200MB)
  - Stage 3 (dashboard-runtime): dotnet/aspnet:8.0, Dashboard only (~200MB)
  - Stage 4 (combined): dotnet/aspnet:8.0, API + Dashboard (~250MB)
  - Non-root user (appuser), health checks, optimized layers
  
- `docker-compose.yml`: Local development environment
  - `postgres` service: PostgreSQL 15 with persistent volume
  - `api` service: ASP.NET Core API on port 8080
  - `dashboard` service: Blazor Server on port 8082
  - `migration` service: EF Core migrations job (run once)
  - Network: serviceregistry-network (bridge)
  
- `.dockerignore`: Excludes build outputs, IDEs, specs from build context

**Kubernetes** (`/deployment/kubernetes/`) (Research Topic 11, 12):
- `namespace.yaml`: serviceregistry namespace isolation
- `configmap.yaml`: Application settings (heartbeat intervals, rate limits, logging, API URLs)
- `secret.yaml`: Database connection strings (base64-encoded, template with CHANGE_ME warnings)
- `postgres.yaml`: StatefulSet for PostgreSQL
  - 1 replica with stable network identity
  - PersistentVolumeClaim (10Gi)
  - Headless Service for StatefulSet DNS
  - Health probes (liveness/readiness)
  - Resource requests/limits
  
- `migration-job.yaml`: EF Core migrations (Research Topic 12)
  - Kubernetes Job with restartPolicy: OnFailure
  - Init container: wait-for-postgres (checks connectivity before migration)
  - Runs `dotnet ef database update`
  - Idempotent (safe to re-run)
  - backoffLimit: 3
  
- `api-deployment.yaml`: API Deployment + Service
  - Deployment: 2 replicas (HA), pod anti-affinity (spread across nodes)
  - ClusterIP Service: Exposes API internally on port 8080
  - Health probes: /health endpoint (liveness + readiness)
  - Resource requests/limits: 200m/500m CPU, 256Mi/512Mi memory
  - ConfigMap/Secret mounts for configuration
  - Optional Ingress template (commented out)
  
- `dashboard-deployment.yaml`: Dashboard Deployment + Service
  - Deployment: 2 replicas, pod anti-affinity
  - Service with ClientIP session affinity (sticky sessions for SignalR)
  - Health probes: /health endpoint
  - Resource requests/limits: 200m/400m CPU, 256Mi/512Mi memory
  - ConfigMap/Secret mounts
  - Optional Ingress with nginx affinity annotations (commented out)
  
- `hpa.yaml`: HorizontalPodAutoscaler (Research Topic 11)
  - API HPA: min 2, max 10 replicas, target 70% CPU, 80% memory
  - Dashboard HPA: min 2, max 6 replicas, target 70% CPU, 80% memory
  - Scale-down stabilization: 5 minutes
  - Scale-up policy: 100% increase per minute (API), 50% (Dashboard)

**Infrastructure Notes**:
- All K8s resources use `app.kubernetes.io/*` labels for kubectl filtering
- PostgreSQL uses StatefulSet (not Deployment) for stable storage
- API is stateless (HPA-compatible)
- Dashboard requires sticky sessions for SignalR WebSocket connections
- Migrations run as pre-deployment Job (not init container) to avoid concurrent migration attempts
- Ingress templates include TLS configuration placeholders

#### 5. Quickstart Guide (`quickstart.md`)

Developer onboarding document:
- Prerequisites (SDK, database, tools)
- Build instructions
- Database setup (migrations)
- Configuration (appsettings.json)
- Running the API locally
- Running the Dashboard locally
- Testing endpoints (curl examples)
- Docker quickstart (docker-compose up)
- Kubernetes local development (minikube, kubectl)
- Running tests
- Common troubleshooting

#### 6. Agent Context Update (`.github/copilot-instructions.md`)

Update with comprehensive code generation guidelines:
- **Technology Stack**: C# .NET 8.0, ASP.NET Core, Blazor Server, SignalR, EF Core, PostgreSQL, Docker, Kubernetes, bUnit
- **Architecture Patterns**: Clean architecture, repository pattern, dependency injection, background services
- **Code Generation Rules**: Strong typing (no `var`), SOLID principles, one class per file, ILogger parameter ordering
- **Dashboard Guidelines**: Blazor Server architecture, SignalR subscription patterns, bUnit testing examples
- **Deployment Guidelines**: Docker multi-stage builds, K8s manifest patterns, EF Core migrations in K8s Jobs
- **Testing Frameworks**: xUnit, Moq, FluentAssertions, bUnit
- **Key Dependencies**: All research decisions from Phase 0

### Validation Checklist

After Phase 1 completion:
- [x] All entities from spec mapped to C# models (RegistrationRequest, Service, ServiceChangeHistory)
- [x] All 8 API endpoints have OpenAPI contracts (4 YAML files)
- [x] All validation rules from R6 documented in contracts
- [x] State machines for RegistrationStatus, HealthStatus, DeletionStatus defined
- [x] Database indexes cover all query patterns
- [x] Error responses consistent across all endpoints
- [x] Dashboard pages specified (3 pages: monitoring, approvals, detail)
- [x] SignalR real-time architecture documented (ServiceMonitorHub pattern)
- [x] Docker artifacts complete (Dockerfile multi-stage, docker-compose, .dockerignore)
- [x] Kubernetes manifests complete (8 files: namespace, configmap, secret, postgres, migration, api, dashboard, hpa)
- [x] Quickstart guide covers API, Dashboard, Docker, Kubernetes
- [x] Agent context updated with all technology decisions and patterns

## Phase 2: Task Breakdown

**NOTE**: Phase 2 (task generation) is executed by the `/speckit.tasks` command, NOT by `/speckit.plan`.

The `/speckit.plan` command ends after Phase 1 artifacts are generated. Task breakdown into concrete implementation work items will be performed separately.

## Next Steps

1. ✅ Specification complete (`spec.md`)
2. ✅ Implementation plan complete (`plan.md` - this file)
3. ✅ Execute Phase 0: Generate `research.md`
4. ✅ Execute Phase 1: Generate `data-model.md`, `/contracts/*.yaml`, `quickstart.md`
5. ✅ Update agent context (`.github/copilot-instructions.md`)
6. ✅ Re-validate Constitution Check post-design (all items pass)
7. ⏳ Run `/speckit.tasks` for Phase 2 task breakdown

**Phase 0 & Phase 1 Complete**: All design artifacts generated and validated. Ready for task breakdown.

**Suggested next command**: Run `/speckit.tasks` to generate task breakdown for implementation.
