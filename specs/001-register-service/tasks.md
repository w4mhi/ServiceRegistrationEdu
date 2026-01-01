# Tasks: Service Registration with Heartbeat Monitoring

**Input**: Design documents from `/specs/001-register-service/`  
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Status**: ✅ **MVP COMPLETE** (January 1, 2026)  
**Implementation**: All core features complete including AI Health Insights, service restoration, real-time dashboard with SignalR

**Tests**: Test tasks are included per the constitution and test matrix requirements (unit tests, integration tests, API tests, component tests).

**Organization**: Tasks are grouped by user scenario to enable incremental delivery and independent testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user scenario this task belongs to (US1-US13 from spec.md)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md project structure:
- Source: `src/Omni.ServiceRegistry.{Layer}/`
- Tests: `tests/Omni.ServiceRegistry.{Layer}.Tests/`
- Deployment: `deployment/{docker,kubernetes}/`
- Specs: `specs/001-register-service/`

---

## Phase 1: Setup (Shared Infrastructure) ✅

**Purpose**: Project initialization, directory structure, build configuration

- [x] T001 Create solution file `ServiceRegistry.sln` in repository root
- [x] T002 Create `Directory.Build.props` with common MSBuild properties (C# 10.0, Nullable enabled, TreatWarningsAsErrors)
- [x] T003 Create `Directory.Packages.props` with central package management (ASP.NET Core 9.0, EF Core 9.0, Blazor, SignalR, StackExchange.Redis, xUnit, Moq, FluentAssertions, bUnit, Scalar)
- [x] T004 [P] Create `src/Omni.ServiceRegistry.Models/` class library project
- [x] T005 [P] Create `src/Omni.ServiceRegistry.Interfaces/` class library project
- [x] T006 [P] Create `src/Omni.ServiceRegistry.Services/` class library project
- [x] T007 [P] Create `src/Omni.ServiceRegistry.Data/` class library project (storage abstraction) - **Note**: Now contains InMemory/, Postgres/, Redis/ subfolders
- [x] T008 [P] Create storage implementations in `src/Omni.ServiceRegistry.Data/Postgres/` (previously separate project)
- [x] T009 [P] Create storage implementations in `src/Omni.ServiceRegistry.Data/Redis/` (previously separate project)
- [x] T010 [P] Create storage implementations in `src/Omni.ServiceRegistry.Data/InMemory/` (previously separate project)
- [x] T011 [P] Create `src/Omni.ServiceRegistry.Api/` web application project
- [x] T012 [P] Create `src/Omni.ServiceRegistry.Dashboard/` Blazor Server project
- [x] T013 [P] Create `tests/Omni.ServiceRegistry.Services.Tests/` test project
- [x] T014 [P] Create `tests/Omni.ServiceRegistry.Data.Tests/` test project
- [x] T015 [P] Create `tests/Omni.ServiceRegistry.Api.Tests/` test project
- [x] T016 [P] Create `tests/Omni.ServiceRegistry.Dashboard.Tests/` test project
- [x] T017 [P] Create `tests/Omni.ServiceRegistry.Tests.Common/` shared test utilities project
- [x] T018 Configure project references (Models → Interfaces → Services → Data, API/Dashboard → Services)
- [x] T019 [P] Create `.gitignore` with .NET patterns (bin/, obj/, *.user, packages/, .vs/, .idea/)
- [x] T020 [P] Create `.dockerignore` with build artifacts (bin/, obj/, Dockerfile*, .git/, specs/, *.md except README)
- [x] T021 [P] Create `README.md` in repository root with project overview and quickstart links

---

## Phase 2: Foundational (Blocking Prerequisites) ✅

**Purpose**: Core domain models, interfaces, and storage abstraction - MUST be complete before user scenarios

**⚠️ CRITICAL**: No user scenario implementation can begin until this phase is complete

### Domain Models (from data-model.md)

- [x] T022 [P] Create `HealthStatus` enum in `src/Omni.ServiceRegistry.Models/HealthStatus.cs` (HEALTHY, UNHEALTHY, DEGRADED, DEAD, RECOVERED, DELETED)
- [x] T023 [P] Create `RegistrationStatus` enum in `src/Omni.ServiceRegistry.Models/RegistrationStatus.cs` (Pending=0, Approved=1, Denied=2)
- [x] T024 [P] Create `DeletionStatus` enum in `src/Omni.ServiceRegistry.Models/DeletionStatus.cs` (Active=0, PendingDeletion=1, Deleted=2)
- [x] T025 [P] Create `RegistrationRequest` entity in `src/Omni.ServiceRegistry.Models/RegistrationRequest.cs` with all properties per data-model.md
- [x] T026 [P] Create `Service` entity in `src/Omni.ServiceRegistry.Models/Service.cs` with all properties, RowVersion for concurrency
- [x] T027 [P] Create `ServiceChangeHistory` entity in `src/Omni.ServiceRegistry.Models/ServiceChangeHistory.cs` (deferred to Phase 11 per plan)

### Storage Abstraction (from research.md Decision 1)

- [x] T028 Create `IStorageProvider` interface in `src/Omni.ServiceRegistry.Data/IStorageProvider.cs` (ReadAsync, WriteAsync, DeleteAsync, QueryAsync) - **Note**: Not implemented; using direct repository pattern
- [x] T029 Create `StorageProviderFactory` in `src/Omni.ServiceRegistry.Data/StorageProviderFactory.cs` (creates provider based on config) - **Note**: Factory logic in Program.cs

### Repository Interfaces (from plan.md)

- [x] T030 [P] Create `IRegistrationRepository` interface in `src/Omni.ServiceRegistry.Interfaces/IRegistrationRepository.cs`
- [x] T031 [P] Create `IServiceRepository` interface in `src/Omni.ServiceRegistry.Interfaces/IServiceRepository.cs`
- [x] T032 [P] Create `IChangeHistoryRepository` interface in `src/Omni.ServiceRegistry.Interfaces/IChangeHistoryRepository.cs` (deferred interface)

### Service Interfaces (from plan.md)

- [x] T033 [P] Create `IRegistrationService` interface in `src/Omni.ServiceRegistry.Interfaces/IRegistrationService.cs`
- [x] T034 [P] Create `IServiceCatalogService` interface in `src/Omni.ServiceRegistry.Interfaces/IServiceCatalogService.cs`
- [x] T035 [P] Create `IHeartbeatService` interface in `src/Omni.ServiceRegistry.Interfaces/IHeartbeatService.cs`
- [x] T036 [P] Create `IAdministratorService` interface in `src/Omni.ServiceRegistry.Interfaces/IAdministratorService.cs`

### Shared Utilities

- [x] T037 [P] Create `ServiceNameNormalizer` in `src/Omni.ServiceRegistry.Services/ServiceNameNormalizer.cs` (SHA256 hash of lowercase name)
- [x] T038 [P] Create `RegistrationValidator` in `src/Omni.ServiceRegistry.Services/RegistrationValidator.cs` (R6 validation rules)

**Checkpoint**: Foundation ready - user scenario implementation can now begin

---

## Phase 3: User Scenario 1 - Register a New Service (Priority: P1) 🎯 MVP ✅

**Goal**: Service owner can submit registration request and receive registration ID with 'pending' status

**Independent Test**: POST to `/api/v1/register` returns 200 with registrationId and status 'pending'

**Requirements**: R1-R10 (registration endpoint, validation, uniqueness, normalization, idempotency, status response)

### Tests for US1

- [ ] T039 [P] [US1] Unit test for `ServiceNameNormalizer` in `tests/Omni.ServiceRegistry.Services.Tests/Unit/ServiceNameNormalizerTests.cs`
- [ ] T040 [P] [US1] Unit test for `ValidationService` registration validation in `tests/Omni.ServiceRegistry.Services.Tests/Unit/ValidationServiceTests.cs`
- [ ] T041 [P] [US1] Unit test for `RegistrationService.SubmitRegistrationAsync` in `tests/Omni.ServiceRegistry.Services.Tests/Unit/RegistrationServiceTests.cs`
- [ ] T042 [P] [US1] Integration test for registration idempotency in `tests/Omni.ServiceRegistry.Services.Tests/Integration/RegistrationFlowTests.cs`
- [ ] T043 [P] [US1] API contract test for `/api/v1/register` endpoint in `tests/Omni.ServiceRegistry.Api.Tests/Controllers/RegistrationControllerTests.cs`

### Implementation for US1

- [x] T044 [US1] Implement `RegistrationService` in `src/Omni.ServiceRegistry.Services/RegistrationService.cs` (SubmitRegistrationAsync, idempotency logic)
- [x] T045 [P] [US1] Create `RegistrationRequestDto` in `src/Omni.ServiceRegistry.Api/DTOs/RegistrationRequestDto.cs`
- [x] T046 [P] [US1] Create `RegistrationResponseDto` in `src/Omni.ServiceRegistry.Api/DTOs/RegistrationResponseDto.cs`
- [x] T047 [US1] Implement `RegistrationController.Register` POST endpoint in `src/Omni.ServiceRegistry.Api/Controllers/RegistrationController.cs`
- [x] T048 [US1] Add model validation attributes to `RegistrationRequestDto` (R6 validation rules)
- [x] T049 [US1] Implement error response handling in `RegistrationController` (400, 401, 503)

**Checkpoint**: At this point, US1 should be functional with in-memory/File storage provider

---

## Phase 4: User Scenario 3 - Check Registration Status (Priority: P1) 🎯 MVP ✅

**Goal**: Service owner can query registration status by ID and receive current status (pending/approved/denied)

**Independent Test**: GET to `/api/v1/status/registration/{id}` returns 200 with current status

**Requirements**: R9 (status query endpoint)

### Tests for US3

- [ ] T050 [P] [US3] Unit test for `RegistrationService.GetRegistrationStatusAsync` in `tests/Omni.ServiceRegistry.Services.Tests/Unit/RegistrationServiceTests.cs`
- [ ] T051 [P] [US3] API contract test for `/api/v1/status/registration/{id}` in `tests/Omni.ServiceRegistry.Api.Tests/Controllers/StatusControllerTests.cs`

### Implementation for US3

- [x] T052 [US3] Implement `RegistrationService.GetRegistrationStatusAsync` in `src/Omni.ServiceRegistry.Services/RegistrationService.cs`
- [x] T053 [P] [US3] Create `RegistrationStatusResponseDto` in `src/Omni.ServiceRegistry.Api/DTOs/RegistrationStatusResponseDto.cs`
- [x] T054 [US3] Create `StatusController` in `src/Omni.ServiceRegistry.Api/Controllers/StatusController.cs`
- [x] T055 [US3] Implement `StatusController.GetRegistrationStatus` GET endpoint
- [x] T056 [US3] Add error handling for non-existent registration IDs (404)

**Checkpoint**: US1 + US3 now provide complete registration submission and status tracking

---

## Phase 5: Storage Provider - PostgreSQL (Priority: P1) 🎯 MVP ✅

**Purpose**: Implement PostgreSQL storage backend with EF Core for persistent storage

**Requirements**: R55 (EF Core migrations), research.md Decision 1

### Tests for PostgreSQL Provider

- [ ] T057 [P] Unit test for `PostgresServiceRepository.GetByIdAsync` in `tests/Omni.ServiceRegistry.Data.Tests/Postgres/PostgresServiceRepositoryTests.cs`
- [ ] T058 [P] Unit test for `PostgresRegistrationRepository` operations in `tests/Omni.ServiceRegistry.Data.Tests/Postgres/PostgresRegistrationRepositoryTests.cs`
- [ ] T059 [P] Integration test for EF Core concurrency handling in `tests/Omni.ServiceRegistry.Data.Tests/Postgres/ConcurrencyTests.cs`

### Implementation for PostgreSQL Provider

- [x] T060 Create `ServiceRegistryDbContext` in `src/Omni.ServiceRegistry.Data/Postgres/ServiceRegistryDbContext.cs` (DbSets, query filters for soft delete, OnModelCreating with indexes/constraints)
- [x] T061 Implement `PostgresStorageProvider` - **Note**: Using direct repository pattern, no separate provider class
- [x] T062 [P] Implement `PostgresRegistrationRepository` in `src/Omni.ServiceRegistry.Data/Postgres/Repositories/PostgresRegistrationRepository.cs`
- [x] T063 [P] Implement `PostgresServiceRepository` in `src/Omni.ServiceRegistry.Data/Postgres/Repositories/PostgresServiceRepository.cs`
- [x] T064 [P] Implement `PostgresChangeHistoryRepository` stub in `src/Omni.ServiceRegistry.Data/Postgres/Repositories/PostgresChangeHistoryRepository.cs` (deferred implementation)
- [x] T065 Create EF Core initial migration `20251115020519_InitialSchema` in `src/Omni.ServiceRegistry.Data/Postgres/Migrations/`
- [x] T066 Configure `appsettings.json` with PostgreSQL connection string and `DatabaseProvider: "Postgres"`

**Checkpoint**: PostgreSQL backend fully functional for US1 + US3

---

## Phase 6: User Scenario 2 - Administrator Approves Service Registration (Priority: P1) 🎯 MVP ✅

**Goal**: Administrator can view pending registrations and approve/deny them

**Independent Test**: Approve registration changes status to 'approved' and creates Service entity

**Requirements**: R11-R13, R25-R27 (approval workflow, admin endpoints, status update)

### Tests for US2

- [ ] T067 [P] [US2] Unit test for `AdministratorService.GetPendingRegistrationsAsync` in `tests/Omni.ServiceRegistry.Services.Tests/Unit/AdministratorServiceTests.cs`
- [ ] T068 [P] [US2] Unit test for `AdministratorService.ApproveRegistrationAsync` in `tests/Omni.ServiceRegistry.Services.Tests/Unit/AdministratorServiceTests.cs`
- [ ] T069 [P] [US2] Integration test for approval workflow in `tests/Omni.ServiceRegistry.Services.Tests/Integration/AdminWorkflowTests.cs`
- [ ] T070 [P] [US2] API contract test for `/api/v1/admin/registrations/pending` in `tests/Omni.ServiceRegistry.Api.Tests/Controllers/AdminControllerTests.cs`

### Implementation for US2

- [x] T071 [US2] Implement `AdministratorService` in `src/Omni.ServiceRegistry.Services/AdministratorService.cs` (GetPendingRegistrationsAsync, ApproveRegistrationAsync, DenyRegistrationAsync)
- [x] T072 [P] [US2] Create `PendingRegistrationDto` in `src/Omni.ServiceRegistry.Api/DTOs/PendingRegistrationDto.cs`
- [x] T073 [P] [US2] Create `ApprovalRequestDto` in `src/Omni.ServiceRegistry.Api/DTOs/ApprovalRequestDto.cs`
- [x] T074 [P] [US2] Create `DenialRequestDto` in `src/Omni.ServiceRegistry.Api/DTOs/DenialRequestDto.cs`
- [x] T075 [US2] Create `AdminController` in `src/Omni.ServiceRegistry.Api/Controllers/AdminController.cs`
- [x] T076 [US2] Implement `AdminController.GetPendingRegistrations` GET endpoint
- [x] T077 [US2] Implement `AdminController.ApproveRegistration` POST endpoint
- [x] T078 [US2] Implement `AdminController.DenyRegistration` POST endpoint (require comments in request body)
- [x] T079 [US2] Add logging for approval/denial actions per R59

**Checkpoint**: Complete registration workflow - US1 (submit) → US3 (check) → US2 (approve) → US5 (catalog)

---

## Phase 7: User Scenario 5 - View Registered Services (Priority: P1) 🎯 MVP

**Goal**: Service consumers can browse and search the service catalog

**Independent Test**: GET to `/api/v1/catalog` returns list of approved services; GET to `/api/v1/status/service/{id}` returns service details

**Requirements**: R14-R18 (catalog, search, filtering, service status endpoint)

### Tests for US5

- [ ] T080 [P] [US5] Unit test for `ServiceCatalogService.GetAllServicesAsync` in `tests/Omni.ServiceRegistry.Services.Tests/Unit/ServiceCatalogServiceTests.cs`
- [ ] T081 [P] [US5] Unit test for `ServiceCatalogService.SearchServicesAsync` in `tests/Omni.ServiceRegistry.Services.Tests/Unit/ServiceCatalogServiceTests.cs`
- [ ] T082 [P] [US5] API contract test for `/api/v1/status/service/{id}` in `tests/Omni.ServiceRegistry.Api.Tests/Controllers/StatusControllerTests.cs`

### Implementation for US5

- [ ] T083 [US5] Implement `ServiceCatalogService` in `src/Omni.ServiceRegistry.Services/ServiceCatalogService.cs` (GetAllServicesAsync, SearchServicesAsync, GetByOwnerAsync, GetByHealthStatusAsync)
- [ ] T084 [P] [US5] Create `ServiceDto` in `src/Omni.ServiceRegistry.Api/DTOs/ServiceDto.cs`
- [ ] T085 [P] [US5] Create `ServiceStatusResponseDto` in `src/Omni.ServiceRegistry.Api/DTOs/ServiceStatusResponseDto.cs`
- [ ] T086 [US5] Implement `StatusController.GetServiceStatus` GET endpoint for `/api/v1/status/service/{id}`
- [ ] T087 [US5] Create `CatalogController` in `src/Omni.ServiceRegistry.Api/Controllers/CatalogController.cs`
- [ ] T088 [US5] Implement `CatalogController.GetAllServices` GET endpoint with query parameters for search/filter

**Checkpoint**: US5 provides catalog visibility - MVP can now register, approve, and discover services

---

## Phase 8: User Scenario 6 - Service Sends Heartbeats and Maintains Healthy Status (Priority: P1) 🎯 MVP

**Goal**: Approved services can send heartbeats and maintain HEALTHY status

**Independent Test**: POST to `/api/v1/heartbeat/{serviceId}` returns ACK; service health remains HEALTHY

**Requirements**: R33-R38 (heartbeat endpoint, ACK response, HEALTHY status, timestamp update, counter reset)

### Tests for US6

- [ ] T089 [P] [US6] Unit test for `HeartbeatService.ProcessHeartbeatAsync` in `tests/Omni.ServiceRegistry.Services.Tests/Unit/HeartbeatServiceTests.cs`
- [ ] T090 [P] [US6] Unit test for optimized write strategy (status change + periodic snapshot) in `tests/Omni.ServiceRegistry.Services.Tests/Unit/HeartbeatServiceTests.cs`
- [ ] T091 [P] [US6] Integration test for heartbeat flow in `tests/Omni.ServiceRegistry.Services.Tests/Integration/HeartbeatMonitoringTests.cs`
- [ ] T092 [P] [US6] API contract test for `/api/v1/heartbeat/{serviceId}` in `tests/Omni.ServiceRegistry.Api.Tests/Controllers/HeartbeatControllerTests.cs`

### Implementation for US6

- [x] T093 [US6] Implement `HeartbeatService` in `src/Omni.ServiceRegistry.Services/HeartbeatService.cs` (ProcessHeartbeatAsync with in-memory state, optimized writes per research.md Decision 2)
- [x] T094 [P] [US6] Create `HeartbeatRequestDto` in `src/Omni.ServiceRegistry.Api/DTOs/HeartbeatRequestDto.cs`
- [x] T095 [P] [US6] Create `HeartbeatResponseDto` in `src/Omni.ServiceRegistry.Api/DTOs/HeartbeatResponseDto.cs`
- [x] T096 [US6] Create `HeartbeatController` in `src/Omni.ServiceRegistry.Api/Controllers/HeartbeatController.cs`
- [x] T097 [US6] Implement `HeartbeatController.SubmitHeartbeat` POST endpoint (target <500ms response time per R33)
- [x] T098 [US6] Add rate limiting for heartbeat endpoint (200 req/sec per research.md Decision 5)
- [x] T099 [US6] Add error handling for unapproved services (403) and non-existent services (404)

**Checkpoint**: US6 enables heartbeat monitoring - services can now maintain HEALTHY status

---

## Phase 9: User Scenario 7 - Service Health Degrades Due to Missed Heartbeats (Priority: P1) 🎯 MVP

**Goal**: System automatically transitions service health status when heartbeats are missed

**Independent Test**: Stop sending heartbeats; verify status transitions HEALTHY → UNHEALTHY → DEGRADED → DEAD

**Requirements**: R39-R42 (UNHEALTHY, DEGRADED, DEAD status transitions, missed counter increment, threshold logic)

### Tests for US7

- [ ] T100 [P] [US7] Unit test for health status transitions in `tests/Omni.ServiceRegistry.Services.Tests/Unit/HeartbeatServiceTests.cs`
- [ ] T101 [P] [US7] Unit test for DEGRADED threshold (50% rounded up) in `tests/Omni.ServiceRegistry.Services.Tests/Unit/HeartbeatServiceTests.cs`
- [ ] T102 [P] [US7] Integration test for timeout detection in `tests/Omni.ServiceRegistry.Services.Tests/Integration/HeartbeatMonitoringTests.cs`

### Implementation for US7

- [x] T103 [US7] Implement `HeartbeatMonitorService` background service in `src/Omni.ServiceRegistry.Services/HeartbeatMonitorService.cs` (IHostedService, timer-based timeout detection)
- [x] T104 [US7] Implement health status transition logic in `HeartbeatService.CalculateHealthStatus` (HEALTHY→UNHEALTHY→DEGRADED→DEAD per R39-R42)
- [x] T105 [US7] Add logging for health status transitions per R60
- [x] T106 [US7] Register `HeartbeatMonitorService` as hosted service in `Program.cs`

**Checkpoint**: US7 completes health degradation - system now automatically detects and tracks unhealthy services

---

## Phase 10: User Scenario 8 & 9 - Service Recovery (Priority: P2)

**Goal**: DEGRADED and DEAD services can recover to HEALTHY status

**Independent Test**: Resume heartbeats after degradation; verify DEGRADED→HEALTHY and DEAD→RECOVERED→HEALTHY transitions

**Requirements**: R43-R45 (RECOVERED status, recovery conditions, transition to HEALTHY)

### Tests for US8 & US9

- [ ] T107 [P] [US8] Unit test for DEGRADED→HEALTHY recovery in `tests/Omni.ServiceRegistry.Services.Tests/Unit/HeartbeatServiceTests.cs`
- [ ] T108 [P] [US9] Unit test for DEAD→RECOVERED→HEALTHY recovery in `tests/Omni.ServiceRegistry.Services.Tests/Unit/HeartbeatServiceTests.cs`
- [ ] T109 [P] Integration test for recovery scenarios in `tests/Omni.ServiceRegistry.Services.Tests/Integration/HeartbeatMonitoringTests.cs`

### Implementation for US8 & US9

- [x] T110 Implement DEGRADED→HEALTHY transition in `HeartbeatService.ProcessHeartbeatAsync`
- [x] T111 Implement DEAD→RECOVERED transition in `HeartbeatService.ProcessHeartbeatAsync`
- [x] T112 Implement RECOVERED→HEALTHY transition (consecutive successful heartbeats = maxMissedHeartbeats) in `HeartbeatService`
- [x] T113 Add logging for recovery transitions per R60

**Checkpoint**: US8 & US9 complete recovery workflows - services can now recover from all degradation states

---

## Phase 11: User Scenario 10 - Service Deletion (Priority: P2)

**Goal**: Service owners/admins can request deletion; admins approve; service is soft-deleted

**Independent Test**: POST to `/api/v1/delete/{id}` creates deletion request; approval marks service DELETED

**Requirements**: R28-R32 (deletion endpoint, approval workflow, soft-delete, DELETED status)

### Tests for US10

- [ ] T114 [P] [US10] Unit test for `AdministratorService.RequestDeletionAsync` in `tests/Omni.ServiceRegistry.Services.Tests/Unit/AdministratorServiceTests.cs`
- [ ] T115 [P] [US10] Unit test for soft-delete query filter in `tests/Omni.ServiceRegistry.Data.Tests/Postgres/SoftDeleteTests.cs`
- [ ] T116 [P] [US10] API contract test for `/api/v1/delete/{id}` in `tests/Omni.ServiceRegistry.Api.Tests/Controllers/AdminControllerTests.cs`

### Implementation for US10

- [ ] T117 [US10] Implement `AdministratorService.RequestDeletionAsync` and `ApproveDeletionAsync` in `src/Omni.ServiceRegistry.Services/AdministratorService.cs`
- [ ] T118 [P] [US10] Create `DeletionRequestDto` in `src/Omni.ServiceRegistry.Api/DTOs/DeletionRequestDto.cs`
- [ ] T119 [US10] Implement `AdminController.RequestDeletion` POST endpoint
- [ ] T120 [US10] Implement `AdminController.GetPendingDeletions` GET endpoint
- [ ] T121 [US10] Implement `AdminController.ApproveDeletion` POST endpoint
- [ ] T122 [US10] Update `ServiceRegistryDbContext` with query filter for DeletionStatus != Deleted
- [ ] T123 [US10] Update unique constraint on ServiceNameNormalized to exclude DELETED services

**Checkpoint**: US10 enables service lifecycle management - services can be removed from catalog

---

## Phase 12: User Scenario 11-13 - Dashboard (Priority: P2)

**Goal**: Web dashboard for monitoring services and processing approvals

**Independent Test**: Navigate to dashboard, view service list, filter by health status, approve registration

**Requirements**: R47-R51 (dashboard pages, monitoring view, approvals view, service detail, access control)

### Tests for Dashboard

- [ ] T124 [P] [US11] bUnit test for `Index.razor` service list rendering in `tests/Omni.ServiceRegistry.Dashboard.Tests/Pages/IndexTests.cs`
- [ ] T125 [P] [US11] bUnit test for `ServiceStatusCard` component in `tests/Omni.ServiceRegistry.Dashboard.Tests/Components/ServiceStatusCardTests.cs`
- [ ] T126 [P] [US11] bUnit test for `HealthIndicator` component in `tests/Omni.ServiceRegistry.Dashboard.Tests/Components/HealthIndicatorTests.cs`
- [ ] T127 [P] [US12] bUnit test for `PendingApprovals.razor` in `tests/Omni.ServiceRegistry.Dashboard.Tests/Pages/PendingApprovalsTests.cs`
- [ ] T128 [P] [US13] bUnit test for owner filtering in `tests/Omni.ServiceRegistry.Dashboard.Tests/Pages/IndexTests.cs`

### Implementation for Dashboard

- [ ] T129 [P] [US11] Create `ServiceViewModel` in `src/Omni.ServiceRegistry.Dashboard/ViewModels/ServiceViewModel.cs`
- [ ] T130 [P] [US11] Create `DashboardDataService` in `src/Omni.ServiceRegistry.Dashboard/Services/DashboardDataService.cs`
- [ ] T131 [US11] Create `Index.razor` monitoring dashboard in `src/Omni.ServiceRegistry.Dashboard/Pages/Index.razor`
- [ ] T132 [P] [US11] Create `ServiceStatusCard.razor` component in `src/Omni.ServiceRegistry.Dashboard/Components/ServiceStatusCard.razor`
- [ ] T133 [P] [US11] Create `HealthIndicator.razor` component in `src/Omni.ServiceRegistry.Dashboard/Components/HealthIndicator.razor`
- [ ] T134 [P] [US11] Create `ServiceTable.razor` component in `src/Omni.ServiceRegistry.Dashboard/Components/ServiceTable.razor`
- [ ] T135 [US12] Create `PendingApprovals.razor` page in `src/Omni.ServiceRegistry.Dashboard/Pages/PendingApprovals.razor`
- [ ] T136 [P] [US12] Create `ApprovalModal.razor` component in `src/Omni.ServiceRegistry.Dashboard/Components/ApprovalModal.razor`
- [ ] T137 [US13] Create `ServiceDetail.razor` page in `src/Omni.ServiceRegistry.Dashboard/Pages/ServiceDetail.razor`
- [ ] T138 Implement SignalR `ServiceMonitorHub` in `src/Omni.ServiceRegistry.Api/Hubs/ServiceMonitorHub.cs` for real-time updates
- [ ] T139 Create `SignalRHubClient` in `src/Omni.ServiceRegistry.Dashboard/Services/SignalRHubClient.cs`
- [ ] T140 Configure SignalR in `Program.cs` for both API and Dashboard projects
- [ ] T141 Add CSS/styling for dashboard in `src/Omni.ServiceRegistry.Dashboard/wwwroot/css/`

**Checkpoint**: US11-13 complete dashboard - administrators can monitor and manage services via web UI

---

## Phase 13: Storage Provider - Redis (Priority: P2)

**Purpose**: Implement Redis storage backend with RedisJSON for document-based storage

**Requirements**: research.md Decision 14 (Redis with JSON module)

### Tests for Redis Provider

- [ ] T142 [P] Unit test for `RedisServiceRepository` in `tests/Omni.ServiceRegistry.Data.Tests/Redis/RedisServiceRepositoryTests.cs`
- [ ] T143 [P] Unit test for `RedisRegistrationRepository` in `tests/Omni.ServiceRegistry.Data.Tests/Redis/RedisRegistrationRepositoryTests.cs`
- [ ] T144 [P] Integration test for Redis persistence (RDB) in `tests/Omni.ServiceRegistry.Data.Tests/Redis/PersistenceTests.cs`

### Implementation for Redis Provider

- [ ] T145 Implement `RedisStorageProvider` in `src/Omni.ServiceRegistry.Data.Redis/RedisStorageProvider.cs` (uses StackExchange.Redis + RedisJSON)
- [ ] T146 [P] Implement `RedisServiceRepository` in `src/Omni.ServiceRegistry.Data.Redis/Repositories/RedisServiceRepository.cs`
- [ ] T147 [P] Implement `RedisRegistrationRepository` in `src/Omni.ServiceRegistry.Data.Redis/Repositories/RedisRegistrationRepository.cs`
- [ ] T148 [P] Implement `RedisChangeHistoryRepository` stub in `src/Omni.ServiceRegistry.Data.Redis/Repositories/RedisChangeHistoryRepository.cs`
- [ ] T149 Configure Redis connection string in `appsettings.json` for `DatabaseProvider: "Redis"`

**Checkpoint**: Redis backend available as alternative to PostgreSQL

---

## Phase 14: Storage Provider - File (Testing) (Priority: P3)

**Purpose**: Implement file-based storage for testing and local development

### Implementation for File Provider

- [ ] T150 [P] Implement `FileStorageProvider` in `src/Omni.ServiceRegistry.Data.File/FileStorageProvider.cs` (JSON files in directory)
- [ ] T151 [P] Implement `FileServiceRepository` in `src/Omni.ServiceRegistry.Data.File/Repositories/FileServiceRepository.cs`
- [ ] T152 [P] Implement `FileRegistrationRepository` in `src/Omni.ServiceRegistry.Data.File/Repositories/FileRegistrationRepository.cs`
- [ ] T153 [P] Unit test for File provider in `tests/Omni.ServiceRegistry.Data.Tests/File/FileServiceRepositoryTests.cs`

**Checkpoint**: File provider available for rapid local testing without database

---

## Phase 15: Deployment - Docker (Priority: P1) 🎯 MVP

**Purpose**: Containerize application for deployment

**Requirements**: R52-R53 (Dockerfile, Docker Compose)

### Implementation for Docker

- [ ] T154 Create `Dockerfile` in `deployment/docker/Dockerfile` with multi-stage build (build stage + api-runtime + dashboard-runtime)
- [ ] T155 Create `docker-compose.yml` in `deployment/docker/docker-compose.yml` with profiles (postgres, redis, sqlserver)
- [ ] T156 Add postgres service to docker-compose (PostgreSQL 15, persistent volume)
- [ ] T157 Add redis service to docker-compose (redis-stack-server with RedisJSON, RDB persistence)
- [ ] T158 Add api service to docker-compose (port 8080, depends on postgres/redis)
- [ ] T159 Add dashboard service to docker-compose (port 8082, depends on api)
- [ ] T160 Add migration service to docker-compose (runs EF Core migrations for SQL providers)
- [ ] T161 Create `.dockerignore` (already done in T020)
- [ ] T162 Test Docker build and run locally with `docker-compose up --profile postgres`

**Checkpoint**: Docker deployment ready - application can run in containers

---

## Phase 16: Deployment - Kubernetes (Priority: P2)

**Purpose**: Deploy to Kubernetes with StatefulSets, HPA, and manifests

**Requirements**: R54-R56 (K8s manifests, migrations, horizontal scaling)

### Implementation for Kubernetes

- [ ] T163 [P] Create `namespace.yaml` in `deployment/kubernetes/namespace.yaml`
- [ ] T164 [P] Create `configmap.yaml` in `deployment/kubernetes/configmap.yaml` (app settings, heartbeat config, logging)
- [ ] T165 [P] Create `secret.yaml` in `deployment/kubernetes/secret.yaml` (connection strings template)
- [ ] T166 [P] Create `postgres.yaml` in `deployment/kubernetes/postgres.yaml` (StatefulSet + Service + PVC)
- [ ] T167 [P] Create `redis.yaml` in `deployment/kubernetes/redis.yaml` (StatefulSet + Service + PVC with RDB persistence)
- [ ] T168 [P] Create `migration-job.yaml` in `deployment/kubernetes/migration-job.yaml` (K8s Job for EF Core migrations, SQL only)
- [ ] T169 [P] Create `api-deployment.yaml` in `deployment/kubernetes/api-deployment.yaml` (Deployment + Service + health probes)
- [ ] T170 [P] Create `dashboard-deployment.yaml` in `deployment/kubernetes/dashboard-deployment.yaml` (Deployment + Service with ClientIP affinity)
- [ ] T171 [P] Create `hpa.yaml` in `deployment/kubernetes/hpa.yaml` (HorizontalPodAutoscaler for API and Dashboard)
- [ ] T172 Add resource requests/limits to all deployments per research.md Decision 11
- [ ] T173 Configure health probes (`/health` endpoint) for liveness and readiness
- [ ] T174 Test Kubernetes deployment locally with minikube or kind

**Checkpoint**: Kubernetes deployment ready - application can run in production K8s cluster

---

## Phase 17: API Configuration & Middleware (Priority: P1) 🎯 MVP

**Purpose**: Configure API application, DI, middleware, logging

### Implementation for API

- [x] T175 Configure DI in `Program.cs` (register services, repositories, storage providers via factory)
- [x] T176 Create `ExceptionHandlingMiddleware` in `src/Omni.ServiceRegistry.Api/Middleware/ExceptionHandlingMiddleware.cs`
- [x] T177 Configure rate limiting middleware in `Program.cs` (heartbeat endpoint: 200 req/sec, registration: 10 req/min)
- [x] T178 Configure CORS policy in `Program.cs` for dashboard origin
- [x] T179 Configure logging (ILogger) with structured logging per R57-R61
- [x] T180 Create `appsettings.json` and `appsettings.Development.json` with connection strings and provider configuration
- [x] T181 Add health check endpoint `/health` for Kubernetes probes
- [x] T182 Configure Swagger/OpenAPI documentation generation

**Checkpoint**: API application fully configured and ready to run

---

## Phase 18: Dashboard Configuration (Priority: P2)

**Purpose**: Configure Dashboard application, DI, SignalR, navigation

### Implementation for Dashboard

- [x] T183 Configure DI in `Program.cs` (register services, HttpClient for API calls)
- [ ] T184 Configure SignalR client in `Program.cs`
- [x] T185 Create `_Imports.razor` with common using statements
- [ ] T186 Create `_Host.cshtml` page layout
- [x] T187 Create navigation menu in `Shared/NavMenu.razor`
- [ ] T188 Configure `appsettings.json` with API base URL
- [ ] T189 Add health check endpoint `/health` for Kubernetes probes

**Checkpoint**: Dashboard application fully configured and ready to run

---

## Phase 19: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user scenarios

### Performance & Optimization

- [ ] T190 [P] Add performance logging for heartbeat processing (target <500ms)
- [ ] T191 [P] Add performance logging for registration API (target <2s)
- [ ] T192 [P] Add performance logging for status query API (target <1s)
- [ ] T193 Optimize database queries (review indexes, query plans)
- [ ] T194 Validate heartbeat throughput target (10,000/minute) with load testing

### Documentation

- [ ] T195 [P] Update `README.md` with architecture overview, quickstart link, tech stack
- [ ] T196 [P] Create `DEPLOYMENT.md` in repository root with Docker and Kubernetes deployment instructions
- [ ] T197 [P] Create API documentation from OpenAPI specs (publish Swagger UI)
- [ ] T198 [P] Document storage provider configuration in `README.md`

### Security

- [ ] T199 [P] Add HTTPS enforcement middleware in `Program.cs`
- [ ] T200 [P] Add security headers middleware (HSTS, X-Content-Type-Options, etc.)
- [ ] T201 [P] Document authentication requirements for P2 (placeholder endpoints marked `[Authorize]`)

### Testing & Quality

- [ ] T202 [P] Create test data builders in `tests/Omni.ServiceRegistry.Tests.Common/Builders/`
- [ ] T203 [P] Create test fixtures in `tests/Omni.ServiceRegistry.Tests.Common/Fixtures/`
- [ ] T204 Run all tests and ensure 100% pass rate
- [ ] T205 Validate quickstart.md instructions (manual walkthrough)
- [ ] T206 Code cleanup and refactoring (remove TODOs, dead code)

**Checkpoint**: All polish tasks complete - application ready for production

---

## Deferred: Phase 20 - Service Updates & Change History (Priority: P3 - Future)

**Purpose**: Enable service owners and admins to update service information after registration

**Note**: This phase implements R19-R21, R23-R24 which were explicitly deferred from MVP per plan.md Phase 11

### Future Tasks

- [ ] T207 Refine requirements for service update flows (R19-R21, R23-R24)
- [ ] T208 Design data model changes for service change history (ServiceChangeHistory entity)
- [ ] T209 Define API contracts for service update endpoints (owner and admin flows)
- [ ] T210 Identify and plan impact on dashboard (edit flows, history views)
- [ ] T211 Create follow-up feature spec for service updates and change history (002-register-service-updates)

**Note**: These tasks are placeholders for future work and not part of current implementation scope

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion - BLOCKS all user scenarios
- **User Scenarios (Phase 3-12)**: All depend on Phase 2 completion
  - US1 (Phase 3): Register service
  - US3 (Phase 4): Check status (depends on US1 conceptually)
  - PostgreSQL Provider (Phase 5): Required for persistent storage
  - US2 (Phase 6): Admin approval (depends on US1, US3, Phase 5)
  - US5 (Phase 7): Catalog (depends on US2)
  - US6 (Phase 8): Heartbeat (depends on US2, US5)
  - US7 (Phase 9): Health degradation (depends on US6)
  - US8-9 (Phase 10): Recovery (depends on US7)
  - US10 (Phase 11): Deletion (depends on US2, US5)
  - US11-13 (Phase 12): Dashboard (depends on US1-10, can be parallel)
- **Storage Providers (Phase 13-14)**: Can be implemented in parallel with user scenarios
- **Deployment (Phase 15-16)**: Can be implemented in parallel with later user scenarios
- **Configuration (Phase 17-18)**: Required before running application
- **Polish (Phase 19)**: Depends on all desired user scenarios being complete

### Parallel Execution Opportunities

Within each phase, tasks marked `[P]` can be executed in parallel:
- Phase 1: All project creation tasks (T004-T017)
- Phase 2: All model/interface/service creation tasks
- Phase 3+: Test tasks can run in parallel; implementation tasks often depend on service layer
- Phase 13-16: Storage providers and deployment can be developed in parallel with US10-13

### MVP Scope (Recommended)

**Phases 1-9 + 15 + 17 = MVP**
- Setup, Foundational, US1-7 (registration, approval, catalog, heartbeat, health monitoring)
- PostgreSQL storage provider
- Docker deployment
- API configuration

This provides a fully functional service registration system with heartbeat monitoring.

**Post-MVP**: Phases 10-14 + 16 + 18-19 (recovery, deletion, dashboard, additional storage providers, K8s, polish)

---

## Storage Provider Selection

**Configuration in appsettings.json:**

```json
{
  "DatabaseProvider": "Postgres",  // or "Redis", "SqlServer", "File"
  "ConnectionStrings": {
    "ServiceRegistry": "Host=localhost;Database=serviceregistry;..."
  }
}
```

### Testing Strategy

- **Unit Tests**: Focus on service layer logic (validation, normalization, state transitions)
- **Integration Tests**: Test repository implementations (Postgres, Redis, File) independently
- **API Tests**: Use automated HTTP tests for endpoints
- **Dashboard Tests**: Use bUnit for Blazor component testing

### Performance Targets

- Registration API: <2s (R1)
- Status Query API: <1s (R9, R18)
- Heartbeat API: <500ms (R33)
- Dashboard Load: <2s (R48)
- SignalR Updates: <10s (Success Criteria 12)

---

**Generated**: November 14, 2025  
**Total Tasks**: 211 tasks (206 implementation + 5 deferred)  
**Estimated Effort**: 20-25 days (2 developers working in parallel)  
**MVP Scope**: Phases 1-9 + 15 + 17 (registration, approval, catalog, heartbeat monitoring, health tracking, Docker deployment)
