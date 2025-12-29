# Data Model: Service Registration

**Date**: November 9, 2025  
**Feature**: Service Registration with Heartbeat Monitoring  
**Purpose**: Define database schema, entities, relationships, and constraints

## Entity Relationship Diagram

```
┌─────────────────────────┐
│  RegistrationRequest    │
├─────────────────────────┤
│ RegistrationId (PK)     │
│ ServiceName             │
│ ServiceNameNormalized   │◄─── Unique constraint
│ Description             │
│ Owner                   │
│ ContactEmail            │
│ Endpoints (JSON)        │
│ HeartbeatTimeout        │
│ MaxMissedHeartbeats     │
│ Status (enum)           │
│ SubmittedDate           │
│ ApprovedDeniedDate      │
│ ApprovedDeniedBy        │
│ Comments                │
└─────────────────────────┘
         │ 1:1
         │ (after approval)
         ▼
┌─────────────────────────┐         ┌─────────────────────────┐
│       Service           │1       N│  ServiceChangeHistory   │
├─────────────────────────┤─────────├─────────────────────────┤
│ ServiceId (PK)          │         │ ChangeId (PK)           │
│ ServiceName             │         │ ServiceId (FK)          │
│ ServiceNameNormalized   │◄─── Unique constraint (where not deleted)
│ Description             │         │ ChangedFields           │
│ Owner                   │         │ PreviousValue           │
│ ContactEmail            │         │ NewValue                │
│ Endpoints (JSON)        │         │ ChangedBy               │
│ HeartbeatTimeout        │         │ ChangeTimestamp         │
│ MaxMissedHeartbeats     │         └─────────────────────────┘
│ HealthStatus (enum)     │
│ DeletionStatus (enum)   │
│ DeletionRequestedDate   │
│ DeletionRequestedBy     │
│ DeletionApprovedDate    │
│ DeletionApprovedBy      │
│ MissedHeartbeatCounter  │
│ LastHeartbeatTimestamp  │
│ LastHeartbeatClientStatus│
│ RegistrationDate        │
│ LastUpdatedDate         │
│ RegisteredBy            │
│ RowVersion (concurrency)│
└─────────────────────────┘
```

## Entities

### 1. RegistrationRequest

**Purpose**: Tracks service registration submissions pending administrator approval.

| Column Name | Type | Constraints | Description |
|-------------|------|-------------|-------------|
| RegistrationId | UNIQUEIDENTIFIER | PRIMARY KEY | Unique identifier for registration request |
| ServiceName | NVARCHAR(50) | NOT NULL | Original service name as provided by requester |
| ServiceNameNormalized | NVARCHAR(64) | NOT NULL, UNIQUE | SHA256 hash of lowercase service name for uniqueness |
| Description | NVARCHAR(500) | NOT NULL | Purpose and functionality of the service |
| Owner | NVARCHAR(100) | NOT NULL | Team or individual responsible for service |
| ContactEmail | NVARCHAR(255) | NOT NULL | Email address for service owner contact |
| Endpoints | NVARCHAR(MAX) | NOT NULL | JSON array of service URLs/addresses |
| HeartbeatTimeout | INT | NOT NULL, CHECK > 0 | Maximum seconds between heartbeats before marked missed |
| MaxMissedHeartbeats | INT | NOT NULL, CHECK > 0 | Consecutive missed heartbeats before marking DEAD |
| Status | INT | NOT NULL, DEFAULT 0 | Enum: 0=Pending, 1=Approved, 2=Denied |
| SubmittedDate | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | Timestamp when registration submitted |
| ApprovedDeniedDate | DATETIME2 | NULL | Timestamp of approval/denial action |
| ApprovedDeniedBy | NVARCHAR(100) | NULL | Administrator who approved/denied |
| Comments | NVARCHAR(1000) | NULL | Administrator comments (required for denial) |

**Indexes**:
- `IX_RegistrationRequest_ServiceNameNormalized` (UNIQUE)
- `IX_RegistrationRequest_Status` (for pending queue queries)
- `IX_RegistrationRequest_SubmittedDate` (for sorting)

**Validation Rules** (from R6):
- ServiceName: Alphanumeric with hyphens, 3-50 characters, regex: `^[a-zA-Z0-9-]{3,50}$`
- ContactEmail: Valid email format, regex: `^[^@]+@[^@]+\.[^@]+$`
- Endpoints: Valid JSON array of URLs with protocol (http/https)
- HeartbeatTimeout: Positive integer, recommended range 15-300 seconds
- MaxMissedHeartbeats: Positive integer, recommended range 3-20

**State Transitions**:
```
Pending (0) → Approved (1)
Pending (0) → Denied (2)
```

### 2. Service

**Purpose**: Represents approved, active services in the catalog with health monitoring.

| Column Name | Type | Constraints | Description |
|-------------|------|-------------|-------------|
| ServiceId | UNIQUEIDENTIFIER | PRIMARY KEY | Unique identifier assigned after approval |
| ServiceName | NVARCHAR(50) | NOT NULL | Original service name for display |
| ServiceNameNormalized | NVARCHAR(64) | NOT NULL, UNIQUE (filtered) | SHA256 hash for uniqueness (excluding deleted) |
| Description | NVARCHAR(500) | NOT NULL | Service purpose and functionality |
| Owner | NVARCHAR(100) | NOT NULL | Team/individual responsible |
| ContactEmail | NVARCHAR(255) | NOT NULL | Service owner email |
| Endpoints | NVARCHAR(MAX) | NOT NULL | JSON array of service URLs |
| HeartbeatTimeout | INT | NOT NULL, CHECK > 0 | Timeout in seconds |
| MaxMissedHeartbeats | INT | NOT NULL, CHECK > 0 | Max consecutive missed heartbeats |
| HealthStatus | INT | NOT NULL, DEFAULT 0 | Enum: 0=Healthy, 1=Unhealthy, 2=Degraded, 3=Dead, 4=Recovered, 5=Deleted |
| DeletionStatus | INT | NOT NULL, DEFAULT 0 | Enum: 0=Active, 1=PendingDeletion, 2=Deleted |
| DeletionRequestedDate | DATETIME2 | NULL | When deletion was requested |
| DeletionRequestedBy | NVARCHAR(100) | NULL | Who requested deletion |
| DeletionApprovedDate | DATETIME2 | NULL | When deletion was approved |
| DeletionApprovedBy | NVARCHAR(100) | NULL | Administrator who approved deletion |
| MissedHeartbeatCounter | INT | NOT NULL, DEFAULT 0 | Current count of consecutive missed heartbeats |
| LastHeartbeatTimestamp | DATETIME2 | NULL | Timestamp of most recent heartbeat |
| LastHeartbeatClientStatus | NVARCHAR(50) | NULL | Client-reported status (e.g., 'running') |
| RegistrationDate | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | When service was first registered |
| LastUpdatedDate | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | Last modification timestamp |
| RegisteredBy | NVARCHAR(100) | NOT NULL | Who registered the service |
| RowVersion | ROWVERSION | NOT NULL | Optimistic concurrency token |

**Indexes**:
- `IX_Service_ServiceNameNormalized` (UNIQUE WHERE DeletionStatus != 2)
- `IX_Service_HealthStatus` (for catalog filtering)
- `IX_Service_LastHeartbeatTimestamp` (for timeout detection)
- `IX_Service_DeletionStatus` (for active/deleted filtering)
- `IX_Service_Owner` (for owner-based queries)

**Global Query Filter**:
```csharp
modelBuilder.Entity<Service>()
    .HasQueryFilter(s => s.DeletionStatus != DeletionStatus.Deleted);
```

**State Transitions**:

**HealthStatus**:
```
               ┌──────────────────────────────────┐
               │                                  │
               ▼                                  │
         [HEALTHY] ◄─────────┐                   │
               │             │                    │
  missed > 0  │             │ successful         │
               ▼             │                    │
        [UNHEALTHY]          │                    │
               │             │                    │
missed ≥ 50%  │             │                    │
               ▼             │                    │
         [DEGRADED]──────────┘                    │
               │                                  │
missed > max  │                                  │
               ▼                                  │
           [DEAD]                                 │
               │                                  │
 heartbeat rcvd│                                 │
               ▼                                  │
        [RECOVERED]                               │
               │                                  │
 N consecutive │                                 │
   successful  │                                  │
               └──────────────────────────────────┘

N = MaxMissedHeartbeats value
```

**DeletionStatus**:
```
Active (0) → PendingDeletion (1) → Deleted (2)
```

### 3. ServiceChangeHistory

**Purpose**: Audit log of all modifications to service records (requirement R21).

| Column Name | Type | Constraints | Description |
|-------------|------|-------------|-------------|
| ChangeId | UNIQUEIDENTIFIER | PRIMARY KEY | Unique identifier for change record |
| ServiceId | UNIQUEIDENTIFIER | FOREIGN KEY, NOT NULL | Reference to Service being changed |
| ChangedFields | NVARCHAR(500) | NOT NULL | Comma-separated list of modified fields |
| PreviousValue | NVARCHAR(MAX) | NULL | JSON object with old values |
| NewValue | NVARCHAR(MAX) | NULL | JSON object with new values |
| ChangedBy | NVARCHAR(100) | NOT NULL | User/administrator who made change |
| ChangeTimestamp | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | When change occurred |

**Indexes**:
- `IX_ServiceChangeHistory_ServiceId` (for history queries)
- `IX_ServiceChangeHistory_ChangeTimestamp` (for chronological sorting)
- `IX_ServiceChangeHistory_ChangedBy` (for auditing)

**Foreign Key**:
```
ServiceChangeHistory.ServiceId → Service.ServiceId (ON DELETE NO ACTION)
```

**Example Record**:
```json
{
  "ChangeId": "f3d2a1b0-...",
  "ServiceId": "a1b2c3d4-...",
  "ChangedFields": "Description,ContactEmail",
  "PreviousValue": "{\"Description\": \"Old desc\", \"ContactEmail\": \"old@example.com\"}",
  "NewValue": "{\"Description\": \"New desc\", \"ContactEmail\": \"new@example.com\"}",
  "ChangedBy": "admin@example.com",
  "ChangeTimestamp": "2025-11-09T14:30:00Z"
}
```

## Enumerations

### RegistrationStatus
```csharp
public enum RegistrationStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2
}
```

### HealthStatus
```csharp
public enum HealthStatus
{
    Healthy = 0,
    Unhealthy = 1,
    Degraded = 2,
    Dead = 3,
    Recovered = 4,
    Deleted = 5  // Used for soft-deleted services
}
```

### DeletionStatus
```csharp
public enum DeletionStatus
{
    Active = 0,
    PendingDeletion = 1,
    Deleted = 2
}
```

## Relationships

1. **RegistrationRequest → Service**: One-to-one relationship established upon approval
   - RegistrationRequest remains in database after approval for audit trail
   - Service.RegistrationDate corresponds to RegistrationRequest.ApprovedDeniedDate

2. **Service → ServiceChangeHistory**: One-to-many relationship
   - One service can have multiple change history records
   - Foreign key with NO ACTION to preserve history even if service deleted

## JSON Column Formats

### Endpoints (both RegistrationRequest and Service)
```json
[
  "https://api.service.example.com",
  "https://api-backup.service.example.com:8443/v1"
]
```

**Validation**:
- Array with at least 1 element
- Each element must be valid URL with http/https protocol
- Maximum 10 endpoints per service

### PreviousValue / NewValue (ServiceChangeHistory)
```json
{
  "FieldName1": "value1",
  "FieldName2": "value2"
}
```

## Database Constraints Summary

### Unique Constraints
1. `RegistrationRequest.ServiceNameNormalized` - UNIQUE
2. `Service.ServiceNameNormalized` - UNIQUE WHERE `DeletionStatus != 2`

### Check Constraints
1. `RegistrationRequest.HeartbeatTimeout > 0`
2. `RegistrationRequest.MaxMissedHeartbeats > 0`
3. `Service.HeartbeatTimeout > 0`
4. `Service.MaxMissedHeartbeats > 0`
5. `Service.MissedHeartbeatCounter >= 0`

### Foreign Key Constraints
1. `ServiceChangeHistory.ServiceId → Service.ServiceId` (NO ACTION)

### Default Values
1. `RegistrationRequest.Status = 0` (Pending)
2. `RegistrationRequest.SubmittedDate = GETUTCDATE()`
3. `Service.HealthStatus = 0` (Healthy)
4. `Service.DeletionStatus = 0` (Active)
5. `Service.MissedHeartbeatCounter = 0`
6. `Service.RegistrationDate = GETUTCDATE()`
7. `Service.LastUpdatedDate = GETUTCDATE()`
8. `ServiceChangeHistory.ChangeTimestamp = GETUTCDATE()`

## Concurrency Control

**Strategy**: Optimistic concurrency using `RowVersion` (SQL Server) or equivalent timestamp column.

**Affected Operations**:
- Service updates (description, endpoints, etc.)
- Health status transitions
- Heartbeat processing
- Deletion status changes

**Behavior**: DbUpdateConcurrencyException thrown if row modified between read and update. Application must:
1. Reload entity from database
2. Re-apply changes
3. Retry save operation

## Indexing Strategy

### Performance Considerations
- **RegistrationRequest**: Primary queries by Status (pending queue) and ServiceNameNormalized (duplicate check)
- **Service**: Frequent queries by HealthStatus (catalog filtering), LastHeartbeatTimestamp (timeout detection), Owner (ownership queries)
- **ServiceChangeHistory**: Queries by ServiceId (service history) and ChangeTimestamp (chronological audit)

### Index Maintenance
- Rebuild indexes when fragmentation >30%
- Update statistics after bulk operations (e.g., mass status transitions)
- Monitor query performance for additional index candidates

## Migration Notes

### Initial Schema Creation
1. Create enums as lookup tables or use INT columns with application-level enum mapping
2. Apply unique constraint with filter for ServiceNameNormalized in Service table
3. Set default values and check constraints
4. Create indexes in order: unique constraints, foreign keys, query optimization indexes

### Data Seeding
- No seed data required; empty tables on fresh deployment
- Consider seeding test data for development environments

### Backward Compatibility
- Use EF Core migrations for schema versioning
- Support rolling deployments: additive changes only (no drops until all instances upgraded)
- Version migrations with date prefix: `20251109_InitialSchema`

## Security Considerations

### Sensitive Data
- **ContactEmail**: Contains PII; apply data protection policies
- **Owner**: May contain user identifiers; apply access controls
- **Comments**: May contain denial reasons; restrict to administrators

### Data Retention
- RegistrationRequest: Retain indefinitely for audit trail (or per compliance policy)
- Service: Soft-delete only; physical deletion requires separate archival process
- ServiceChangeHistory: Retain indefinitely per requirement R21

### Access Patterns
- Service owners: Read-only access to their own services
- Administrators: Full CRUD access to all services
- Public catalog: Read-only access to approved, non-deleted services (filtered)

## Performance Targets

| Operation | Target | Notes |
|-----------|--------|-------|
| Insert RegistrationRequest | <100ms | Single row insert |
| Query pending registrations | <500ms | Indexed on Status |
| Update Service health status | <50ms | Batched updates, indexed on LastHeartbeatTimestamp |
| Query service by ID | <10ms | Primary key lookup |
| Search catalog by name | <1s | Indexed on ServiceNameNormalized |
| Insert ServiceChangeHistory | <50ms | Async operation, not blocking main flow |

## Validation Summary

Per requirement R6, all validations implemented at:
1. **Application layer**: DTO validation attributes, FluentValidation rules
2. **Database layer**: CHECK constraints, unique constraints, foreign keys
3. **Domain layer**: Entity validation methods before persistence

**Validation Flow**:
```
API Request → DTO Validation → Service Layer Validation → Domain Entity Validation → Database Constraints
```

---

**Next Steps**:
1. ✅ Data model complete
2. ⏳ Generate API contracts (OpenAPI specifications)
3. ⏳ Create EF Core entity configurations
4. ⏳ Write migration scripts
5. ⏳ Implement repository interfaces
