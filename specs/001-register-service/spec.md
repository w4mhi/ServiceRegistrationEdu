# Feature Specification: Service Registration

**Feature ID**: 1-register-service  
**Created**: November 9, 2025  
**Updated**: January 1, 2026  
**Status**: ✅ Complete (MVP + AI Insights)  

## Overview

Enable services to automatically register themselves into the platform's service registry via REST API, making them discoverable and manageable within the system. This feature provides a centralized, automated way to onboard new services with their metadata and operational details through API integration. The platform monitors registered services through a heartbeat-based health monitoring system to track service availability and health status in real-time. Additionally, **AI-powered health insights** analyze service health patterns using Ollama (phi4 model) to provide intelligent root cause analysis and actionable recommendations.

## Problem Statement

Organizations need an automated way to register and catalog their services as part of the deployment process, along with continuous monitoring of service health. Currently, there is no standardized API-based process for service onboarding and health tracking, leading to inconsistent service metadata, difficulty in tracking what services exist in the environment, and lack of visibility into service health status. Manual registration processes are error-prone and slow down service deployment, while the absence of automated health monitoring makes it difficult to detect and respond to service failures. Furthermore, when health issues occur, administrators lack intelligent analysis tools to quickly understand root causes and correlations across services.

## Target Users

- **Service Owners**: Teams responsible for developing and maintaining services who need to register their services
- **Platform Administrators**: Personnel who oversee service registration approval and manage the service catalog
- **Service Consumers**: Teams or systems that need to discover and interact with registered services
- **Operations/SRE Teams**: Personnel who need intelligent health insights and root cause analysis for faster incident response

## Clarifications

### Session 2025-11-09

- Q: How do administrators get notified of pending registrations? → A: Administrators access Blazor dashboard at http://localhost:5083 to view pending registrations with real-time SignalR updates ✅
- Q: How are services removed from the catalog? → A: Service owners or administrators request deletion via `/api/v1/delete/{id}` which requires approval; services are soft-deleted and marked "DELETED"; re-registration requires new ID ✅
- Q: What authentication/authorization mechanism is used for API access? → A: Deferred to P2 (Priority 2) - marked as Open Issue; assumes existing authentication infrastructure for initial implementation ⚠️
- Q: What observability/logging requirements are needed? → A: ILogger with structured logging (trace, info, warning, error levels); logs all registration, approval, status transitions, heartbeat events ✅
- Q: How are concurrent/duplicate registration requests handled? → A: Idempotency via service name hash (SHA256 of lowercase name); return existing registration ID if pending; database unique constraint on normalized name ✅

### Session 2025-12-31

- Q: How do administrators get intelligent insights about service health? → A: AI Health Insights using Ollama (phi4 model) analyzes health patterns, correlations, and provides root cause analysis with recommendations ✅
- Q: How are insights triggered? → A: Dashboard "✨ AI Analysis" button triggers global analysis; background analysis on critical events; on-demand per-service analysis via API ✅
- Q: What LLM is used? → A: Ollama with phi4:latest model (14.7B parameters, Q4_K_M quantization) running locally on port 11434 ✅

## User Scenarios & Testing

### Scenario 1: Register a New Service
**Actor**: Service Owner  
**Goal**: Register a new service in the platform

**Flow**:
1. Service owner obtains the registration service endpoint
2. Service owner configures their service's appsettings with registration details (name, description, endpoints, owner contact)
3. Service makes POST request to `/api/v1/register` with the registration model
4. System validates the service information
5. Service receives response with registration ID and status 'pending'
6. Service owner can query `/api/v1/status/registration/{id}` to check status (returns 'pending', 'denied', or 'approved')
7. Platform administrator reviews and approves the registration
8. Registration status changes to 'approved'
9. Service appears in the service catalog

**Expected Outcome**: Service is successfully registered after approval and visible in the catalog with all provided metadata

### Scenario 2: Administrator Approves Service Registration
**Actor**: Platform Administrator  
**Goal**: Review and approve a pending service registration

**Flow**:
1. Platform administrator polls the pending registrations API endpoint to view new registration requests
2. Reviews the service details and metadata
3. Verifies information is complete and accurate
4. Approves or denies the registration via admin API
5. System adds service to the catalog if approved
6. Registration status is updated to 'approved' or 'denied' (visible via status API)

**Expected Outcome**: Approved service is added to the catalog and status is updated to 'approved'

### Scenario 3: Check Registration Status
**Actor**: Service Owner  
**Goal**: Monitor the status of a submitted registration

**Flow**:
1. Service has previously submitted registration and received registration ID
2. Service (or service owner) makes GET request to `/api/v1/status/registration/{id}`
3. System returns current status: 'pending', 'denied', or 'approved'
4. If status is 'pending', service can retry status check later
5. If status is 'approved', service is now in the catalog
6. If status is 'denied', service owner can review denial comments and resubmit

**Expected Outcome**: Service receives accurate current status of the registration request

### Scenario 4: Register Service with Invalid Information
**Actor**: Service Owner  
**Goal**: Attempt to register a service but encounter validation errors

**Flow**:
1. Service owner configures their service with incomplete or invalid registration details
2. Service makes POST request to `/api/v1/register` with the invalid data
3. System validates and identifies errors
4. System returns HTTP 400 with clear error messages indicating what needs correction
5. Service owner corrects the configuration
6. Service retries the registration request
7. System returns registration ID with status 'pending'

**Expected Outcome**: Validation errors are clearly communicated via API response, and the service receives registration ID with 'pending' status after corrections

### Scenario 5: View Registered Services
**Actor**: Service Consumer / Service Owner  
**Goal**: Browse and search for registered services, and check service status

**Flow**:
1. Service consumer accesses the service catalog
2. Views list of all registered services
3. Filters or searches for specific services by name, owner, or other attributes
4. Selects a service to view detailed information
5. Reviews service metadata, endpoints, and operational details
6. Service owner can query `/api/v1/status/service/{id}` to get the current status of their service

**Expected Outcome**: Service consumer can easily find and access information about registered services; service owner can retrieve service status via API

### Scenario 6: Service Sends Heartbeats and Maintains Healthy Status
**Actor**: Registered Service  
**Goal**: Maintain healthy status through regular heartbeat transmission

**Flow**:
1. Service is registered and approved with heartbeat timeout of 30 seconds and max missed heartbeats of 10
2. Service starts sending heartbeats via POST to `/api/v1/heartbeat/{serviceId}` with status 'running'
3. Monitoring service receives heartbeat and returns ACK
4. Monitoring service marks the service as HEALTHY
5. Monitoring service resets missed heartbeat counter to 0
6. Monitoring service records timestamp of last heartbeat
7. Service continues sending heartbeats within the 30-second timeout period
8. Each heartbeat resets the missed counter and updates the timestamp

**Expected Outcome**: Service maintains HEALTHY status as long as heartbeats are received within timeout periods

### Scenario 7: Service Health Degrades Due to Missed Heartbeats
**Actor**: Registered Service  
**Goal**: Track service health degradation when heartbeats are missed

**Flow**:
1. Service is HEALTHY with heartbeat timeout 30 seconds and max missed heartbeats of 10
2. Service sends heartbeat (SYN), monitoring service responds with ACK
3. Service misses the next heartbeat (exceeds 30-second timeout)
4. Monitoring service marks the service as UNHEALTHY and increments missed heartbeat counter to 1
5. Service misses additional heartbeats until counter reaches 5 (50% of max 10)
6. Monitoring service marks the service as DEGRADED
7. Service misses heartbeats until counter reaches 11 (max + 1)
8. Monitoring service marks the service as DEAD

**Expected Outcome**: Service health status transitions from HEALTHY → UNHEALTHY → DEGRADED → DEAD based on missed heartbeat thresholds

### Scenario 8: Degraded Service Recovers
**Actor**: Registered Service  
**Goal**: Restore service to healthy status after degradation

**Flow**:
1. Service is in DEGRADED state with 5 missed heartbeats (50% of max 10)
2. Service resumes sending heartbeats within the timeout period
3. Monitoring service receives heartbeat and returns ACK
4. Monitoring service immediately marks the service as HEALTHY
5. Monitoring service resets missed heartbeat counter to 0

**Expected Outcome**: DEGRADED service transitions directly to HEALTHY upon receiving successful heartbeat

### Scenario 9: Dead Service Recovers
**Actor**: Registered Service  
**Goal**: Restore dead service through successful heartbeat transmission

**Flow**:
1. Service is in DEAD state with 11 missed heartbeats (exceeded max of 10)
2. Service resumes sending heartbeats
3. Monitoring service receives heartbeat and returns ACK
4. Monitoring service marks the service as RECOVERED
5. Service continues sending 10 consecutive successful heartbeats (equal to max missed heartbeats)
6. No heartbeats are missed during this recovery period
7. Monitoring service marks the service as HEALTHY
8. After becoming HEALTHY, the service follows the same health degradation rules as any other HEALTHY service when heartbeats are missed

**Expected Outcome**: DEAD service transitions to RECOVERED, then to HEALTHY after sending consecutive successful heartbeats equal to the maximum allowed missed heartbeats without any misses

### Scenario 10: Service Deletion
**Actor**: Service Owner or Platform Administrator  
**Goal**: Remove a service from the active catalog

**Flow**:
1. Service owner or administrator identifies a service that needs to be decommissioned
2. Actor makes POST request to `/api/v1/delete/{id}` to request deletion
3. System creates a pending deletion request
4. Administrator reviews and approves the deletion request
5. System marks the service with status "DELETED"
6. Service is removed from active catalog but record is retained
7. If service needs to be re-registered later, a new service ID is issued

**Expected Outcome**: Service is soft-deleted and marked "DELETED", removed from active catalog, historical record retained

### Scenario 11: Administrator Reviews Services via Dashboard
**Actor**: Platform Administrator  
**Goal**: Monitor all registered services and their health status using the web dashboard

**Flow**:
1. Administrator navigates to the dashboard home page
2. Dashboard displays overview metrics (total services, count by health status)
3. Administrator views list of all services with columns: name, owner, health status, last heartbeat
4. Administrator filters services by health status (e.g., show only DEGRADED or DEAD)
5. Administrator clicks on a service to view detailed information
6. Service detail page shows full metadata, heartbeat history, and change history
7. Administrator identifies services requiring attention based on health indicators

**Expected Outcome**: Administrator has comprehensive visibility into all services and can quickly identify health issues

### Scenario 12: Administrator Processes Pending Approvals via Dashboard
**Actor**: Platform Administrator  
**Goal**: Review and approve/deny pending registration and deletion requests through dashboard interface

**Flow**:
1. Administrator navigates to the "Pending Approvals" page in dashboard
2. Dashboard displays two tabs: "Registration Requests" and "Deletion Requests"
3. Administrator selects "Registration Requests" tab
4. List shows pending registrations with submission date, service name, owner, and details
5. Administrator clicks "Review" on a pending registration
6. Modal/page shows full registration details (endpoints, heartbeat config, contact info)
7. Administrator clicks "Approve" button and optionally adds comments
8. System approves registration, assigns service ID, and removes from pending list
9. Administrator can alternatively click "Deny" and must provide comments

**Expected Outcome**: Administrator efficiently processes pending approvals through intuitive dashboard interface, improving approval turnaround time

### Scenario 13: Service Owner Views Their Services via Dashboard
**Actor**: Service Owner  
**Goal**: Monitor health and status of services they own through the dashboard

**Flow**:
1. Service owner logs into dashboard
2. Dashboard automatically filters to show only services owned by the logged-in user
3. Service owner sees their services with health status indicators (green/yellow/red)
4. Service owner clicks on a service experiencing issues (DEGRADED status)
5. Service detail page shows missed heartbeat counter at 5 of 10 maximum
6. Service owner reviews heartbeat history showing when heartbeats were missed
7. Service owner investigates their service to resolve heartbeat transmission issues

**Expected Outcome**: Service owners have self-service visibility into their services' health status and can proactively address issues

## Functional Requirements

### Service Registration

**R1**: The system shall provide a REST API endpoint at `/api/v1/register` (POST) that accepts service registration requests with:
- Service name (unique identifier)
- Service description (purpose and functionality)
- Service owner (team or individual responsible)
- Contact information (email or communication channel)
- Service endpoints (URLs or network addresses)
- Heartbeat timeout (maximum time in seconds allowed between heartbeats before marking as missed)
- Maximum missed heartbeats (number of consecutive missed heartbeats before marking service as DEAD)

**R2**: The system shall validate that service names are unique across the platform before accepting registration

**R3**: The system shall normalize service names to lowercase for uniqueness validation using a hash-based comparison

**R4**: The system shall store both the normalized service name (for uniqueness) and the original service name (for debugging and display purposes)

**R5**: The system shall implement idempotent registration requests by returning the existing registration ID if a pending registration exists for the same normalized service name

**R6**: The system shall validate that all required fields are provided and meet format requirements:
- Service name: alphanumeric with hyphens, 3-50 characters
- Email addresses: valid email format
- URLs: valid URL format with protocol
- Heartbeat timeout: positive integer (seconds)
- Maximum missed heartbeats: positive integer

**R7**: The system shall assign a unique registration identifier (UUID) upon receiving a valid registration request

**R8**: The system shall return the registration ID and status 'pending' in the API response immediately after validation

**R9**: The system shall provide a REST API endpoint at `/api/v1/status/registration/{id}` (GET) that returns the current registration status: 'pending', 'denied', or 'approved'

**R10**: The system shall record the registration timestamp when the registration request is received

**R11**: The system shall submit all service registrations for approval by a platform administrator before the service becomes visible in the catalog

**R12**: The system shall allow platform administrators to review pending service registrations and approve or reject them with optional comments

**R13**: The system shall update the registration status to 'approved' or 'denied' based on administrator action

### Service Catalog

**R14**: The system shall maintain a searchable catalog of all approved registered services

**R15**: The system shall allow users to search services by:
- Service name
- Owner
- Environment
- Keywords in description

**R16**: The system shall display service information including all registration details and metadata

**R17**: The system shall allow filtering services by environment, status, or owner

**R18**: The system shall provide a REST API endpoint at `/api/v1/status/service/{id}` (GET) that returns the current status and details of a registered service

### Service Updates

**R19**: The system shall allow service owners to update service information after registration

**R20**: The system shall allow platform administrators to update any service registration, with all changes logged in the service change history

**R21**: The system shall maintain a history of changes made to service registrations, including:
- What was changed
- Who made the change
- When the change was made

### Access Control

**R22**: The system shall restrict service registration to authenticated and authorized users

**R23**: The system shall allow service owners to update only their own registered services

**R24**: The system shall allow platform administrators to update any service registration, with all changes logged in the service change history

### Administrator Management

**R25**: The system shall provide a REST API endpoint at `/api/v1/admin/registrations/pending` (GET) that returns a list of all pending service registrations for administrator review

**R26**: The system shall provide a REST API endpoint at `/api/v1/admin/registrations/{id}/approve` (POST) that allows administrators to approve a pending registration with optional comments

**R27**: The system shall provide a REST API endpoint at `/api/v1/admin/registrations/{id}/deny` (POST) that allows administrators to deny a pending registration with required comments explaining the denial reason

**R28**: The system shall provide a REST API endpoint at `/api/v1/delete/{id}` (POST) that allows service owners or administrators to request deletion of a registered service

**R29**: The system shall require approval for all deletion requests before marking services as DELETED

**R30**: The system shall soft-delete services by marking them with status "DELETED" rather than removing records from storage

**R31**: The system shall allow DELETED services to be re-registered with a new service ID

**R32**: The system shall maintain DEAD services in DEAD status until explicit deletion is requested and approved

### Heartbeat Monitoring

**R33**: The system shall provide a REST API endpoint at `/api/v1/heartbeat/{serviceId}` (POST) that accepts heartbeat requests from registered services with:
- Service ID
- Client status ('running' or other operational states)

**R34**: The system shall only monitor services that are registered and have an approved status

**R35**: The system shall respond to heartbeat requests with an acknowledgment (ACK) response

**R36**: The system shall mark a service as HEALTHY when:
- The service is approved and registered
- The first heartbeat is received within the timeout period
- Heartbeats continue to be received within the configured timeout period

**R37**: The system shall reset the missed heartbeat counter to 0 when a successful heartbeat is received from a HEALTHY, UNHEALTHY, or DEGRADED service

**R38**: The system shall record the timestamp of the last received heartbeat for each service

**R39**: The system shall mark a service as UNHEALTHY when:
- A heartbeat is not received within the configured timeout period
- The number of missed heartbeats is greater than 0 but less than 50% of the maximum allowed missed heartbeats

**R40**: The system shall increment the missed heartbeat counter when a heartbeat timeout occurs

**R41**: The system shall mark a service as DEGRADED when:
- The number of consecutive missed heartbeats is greater than or equal to 50% of the maximum allowed missed heartbeats, rounded up to the nearest whole heartbeat

**R42**: The system shall mark a service as DEAD when:
- The number of consecutive missed heartbeats exceeds the maximum allowed missed heartbeats

**R43**: The system shall mark a DEAD service as RECOVERED when:
- The service resumes sending heartbeats successfully

**R44**: The system shall transition a RECOVERED service to HEALTHY when:
- The service sends a number of consecutive successful heartbeats equal to the maximum allowed missed heartbeats
- No heartbeats are missed during this period
- During and after this recovery period, RECOVERED services follow the same health degradation rules as HEALTHY services when heartbeats are missed

**R45**: The system shall transition a DEGRADED service directly to HEALTHY when:
- The service resumes sending heartbeats successfully within the timeout period

**R46**: The system shall include the current health status (HEALTHY, UNHEALTHY, DEGRADED, DEAD, RECOVERED, DELETED) in the service status API response

### Dashboard

**R47**: The system shall provide a Blazor-based web dashboard accessible to administrators and service owners

**R48**: The system shall display a service monitoring dashboard showing:
- Total count of registered services
- Count of services by health status (HEALTHY, UNHEALTHY, DEGRADED, DEAD, RECOVERED)
- List of all services with filterable columns (name, owner, status, last heartbeat)
- Real-time or near-real-time health status updates

**R49**: The system shall provide a pending approvals dashboard page showing:
- List of pending service registration requests with submission details
- List of pending service deletion requests with requester information
- Approve/deny actions with comment capability
- Search and filter capabilities

**R50**: The system shall display service detail pages accessible from the monitoring dashboard showing:
- All service metadata (name, description, owner, contact, endpoints)
- Current health status with visual indicators (green/yellow/red)
- Heartbeat history (last 10-50 heartbeats with timestamps)
- Missed heartbeat counter and threshold configuration
- Change history for the service

**R51**: The system shall provide dashboard access control restricting:
- Service owners to view only their own services
- Administrators to view all services and access approval workflows

### Deployment & Infrastructure

**R52**: The system shall provide a Dockerfile for containerizing the application with:
- Multi-stage build for optimized image size
- .NET 8.0 runtime
- Non-root user execution
- Health check endpoint configuration

**R53**: The system shall provide Docker Compose configuration for local development including:
- API service container
- Database container (PostgreSQL or SQL Server)
- Network configuration for service communication
- Volume mounts for database persistence

**R54**: The system shall provide Kubernetes deployment manifests including:
- Deployment for API application with replica configuration
- Service for load balancing
- ConfigMap for application settings
- Secret for sensitive data (database credentials)
- Persistent Volume Claim for database storage

**R55**: The system shall use Entity Framework Core migrations for database schema management with:
- Initial migration creating all tables, indexes, and constraints
- Migration scripts for version control
- Support for both PostgreSQL and SQL Server providers

**R56**: The system shall support horizontal scaling with multiple API instances through:
- Stateless API design (no in-memory session state)
- Database-backed heartbeat queue processing
- Distributed rate limiting consideration

### Observability

**R57**: The system shall implement logging using ILogger with the following levels:
- Trace: Detailed diagnostic information for troubleshooting
- Info: General informational messages about system operation
- Warning: Potentially harmful situations that don't prevent operation
- Error: Error events that might still allow the application to continue running

**R58**: The system shall log all service registration requests with relevant metadata (service name, owner, timestamp)

**R59**: The system shall log all approval/denial actions by administrators

**R60**: The system shall log health status transitions for monitored services

**R61**: The system shall log failed heartbeat attempts and missed heartbeat events

## Success Criteria

1. **Registration API Performance**: Registration API responds with ID and status within 2 seconds
2. **Validation Accuracy**: 100% of invalid service registrations are rejected with clear error messages
3. **Status Check Performance**: Status query API responds within 1 second
4. **Approval Turnaround**: 90% of service registrations are reviewed and approved within 24 hours
5. **Search Performance**: Service searches return results in under 2 seconds
6. **Catalog Completeness**: All approved services are immediately visible in the catalog
7. **User Adoption**: 80% of services are registered within 30 days of feature launch
8. **Data Quality**: 95% of registered services have complete and accurate metadata
9. **Heartbeat Response Time**: Heartbeat API responds with ACK within 500 milliseconds
10. **Health Status Accuracy**: Service health status transitions occur within one timeout period of the triggering condition
11. **Monitoring Reliability**: 99.9% of heartbeats are successfully processed and acknowledged
12. **Dashboard Responsiveness**: Dashboard pages load within 2 seconds
13. **Dashboard Real-time Updates**: Health status changes reflected in dashboard within 10 seconds

## Scope

### In Scope
- REST API endpoint for service registration (`/api/v1/register`)
- REST API endpoint for registration status checking (`/api/v1/status/registration/{id}`)
- REST API endpoint for service status checking (`/api/v1/status/service/{id}`)
- REST API endpoint for heartbeat monitoring (`/api/v1/heartbeat/{serviceId}`)
- REST API endpoints for administrator management (`/api/v1/admin/registrations/pending`, approve, deny)
- REST API endpoint for service deletion (`/api/v1/delete/{id}`) with approval workflow
- Service registration with core metadata including heartbeat configuration
- Service registration approval workflow with administrator polling mechanism
- Service deletion workflow with soft-delete (status marked "DELETED")
- Heartbeat-based health monitoring with status tracking (HEALTHY, UNHEALTHY, DEGRADED, DEAD, RECOVERED, DELETED)
- Automatic health status transitions based on heartbeat reception
- Service catalog and search functionality
- Basic validation of service information
- Update capability for registered services
- Administrator permissions to update any service
- Change history tracking
- Access control for registration and updates
- Basic application logging using ILogger (trace, info, warning, error levels)
- Blazor-based dashboard for service monitoring and administrator management
- Service monitoring dashboard with health status visualization
- Pending approvals dashboard for registration and deletion requests
- Container support with Docker and Docker Compose
- Kubernetes deployment manifests (deployment, service, configmap, secrets)
- Database persistence with EF Core migrations

### Out of Scope
- Automated service discovery from infrastructure
- Advanced health metrics beyond heartbeat (CPU, memory, disk usage)
- Distributed tracing and correlation IDs
- External metrics collection systems (Prometheus, Grafana) - use built-in dashboard
- Service-to-service authentication configuration
- Automated testing or deployment pipelines
- Service dependency graph visualization
- Integration with external service registries or catalogs
- Bulk service registration (multiple services at once)
- Push notifications or email alerts for approval status changes

## Key Entities

### Registration Request
- Registration ID (UUID) - unique identifier for the registration request
- Service Name (original, as provided)
- Service Name Normalized (lowercase hash for uniqueness validation)
- Service Description
- Owner (user/team identifier)
- Contact Email
- Service Endpoints (list of URLs)
- Heartbeat Timeout (seconds)
- Maximum Missed Heartbeats (integer)
- Registration Status (pending/approved/denied)
- Submitted Date
- Approval/Denial Date
- Approved/Denied By (administrator identifier)
- Approval Comments

### Service
- Service ID (UUID) - unique identifier assigned after approval
- Service Name (original, as provided for display)
- Service Name Normalized (lowercase hash for uniqueness)
- Description
- Owner (user/team identifier)
- Contact Email
- Endpoints (list of URLs)
- Heartbeat Timeout (seconds)
- Maximum Missed Heartbeats (integer)
- Current Health Status (HEALTHY/UNHEALTHY/DEGRADED/DEAD/RECOVERED/DELETED)
- Deletion Status (active/pending_deletion/deleted)
- Deletion Requested Date
- Deletion Requested By (user/admin identifier)
- Deletion Approved Date
- Deletion Approved By (administrator identifier)
- Missed Heartbeat Counter (current count)
- Last Heartbeat Timestamp
- Last Heartbeat Client Status ('running' or other)
- Registration Date
- Last Updated Date
- Registered By (user identifier)

### Service Change History
- Change ID
- Service ID
- Changed Field(s)
- Previous Value
- New Value
- Changed By (user identifier)
- Change Timestamp

## Edge Cases & Error Handling

### Edge Cases
1. **Duplicate Service Name**: Service attempts to register with a name that already exists
2. **Service Decommissioning**: A registered service needs to be removed from the catalog
3. **Multiple Environments**: Same service registered in different environments (dev, staging, prod)
4. **Pending Registrations**: Service submits registration while previous registration is still pending approval
5. **Rejected Registration Resubmission**: Service needs to resubmit a rejected registration with corrections
6. **Status Check for Non-existent ID**: Query for registration status with invalid or expired registration ID
7. **Repeated Registration Attempts**: Service repeatedly calls registration endpoint with same data
8. **Heartbeat Before Approval**: Service attempts to send heartbeats before registration is approved
9. **Intermittent Heartbeats**: Service sends heartbeats irregularly, alternating between successful and missed
10. **Heartbeat Timeout During Recovery**: RECOVERED service misses a heartbeat during the recovery period
11. **Zero or Negative Heartbeat Configuration**: Service registers with invalid heartbeat timeout or max missed heartbeats
12. **Simultaneous Health Status Transitions**: Multiple services transitioning states at the same time

### Error Handling
1. **Validation Errors**: Return HTTP 400 with specific, actionable error messages for each invalid field in API response
2. **System Unavailability**: Return HTTP 503 when registration service is temporarily unavailable
3. **Network Failures**: API should implement appropriate timeout handling
4. **Unauthorized Access**: Return HTTP 401/403 when service lacks permission to register
5. **Duplicate Registration**: Return HTTP 409 when service name already exists
6. **Invalid Registration ID**: Return HTTP 404 when querying status for non-existent registration ID
7. **Heartbeat from Unapproved Service**: Return HTTP 403 when service attempts to send heartbeat before approval
8. **Invalid Service ID in Heartbeat**: Return HTTP 404 when heartbeat contains non-existent service ID
9. **Malformed Heartbeat Request**: Return HTTP 400 when heartbeat request is missing required fields or has invalid format

## Assumptions

1. Services have access to the registration API endpoint
2. Services can be configured via appsettings or configuration files
3. Service ownership is managed through existing team/user management structures
4. Service names follow DNS-compatible naming conventions
5. Service endpoints are accessible over HTTP/HTTPS protocols
6. Email is the primary communication channel for service owner contact
7. Platform administrators have elevated permissions for service management
8. Platform administrators are available to review registrations within 24 hours
9. Services can implement polling mechanism to check registration status
10. API authentication is handled through existing authentication infrastructure
11. Services can reliably send heartbeats at their configured intervals
12. Network latency between services and monitoring system is acceptable for heartbeat transmission
13. Services have mechanisms to handle and retry failed heartbeat transmissions
14. Heartbeat timeout values are reasonable (typically 15-300 seconds)
15. Maximum missed heartbeats values are reasonable (typically 3-20)

## Dependencies

1. **Authentication System**: Requires existing API authentication and authorization infrastructure
2. **User Management**: Requires user and team information for ownership and access control
3. **Storage System**: Requires persistent storage for service catalog, registration requests, and change history

## Constraints

1. **Data Volume**: Must support minimum of 1,000 registered services without performance degradation
2. **Concurrent Users**: Must support at least 50 concurrent users performing registration or search operations
3. **Response Time**: All user operations (registration, search, update) must complete within 5 seconds
4. **Availability**: Service registration functionality must maintain 99.5% uptime during business hours
5. **Heartbeat Processing**: Must process at least 10,000 heartbeats per minute without performance degradation
6. **Health Status Update Latency**: Health status changes must be reflected within one timeout period
7. **Monitoring Scalability**: Must support monitoring up to 1,000 services simultaneously

## Risks

1. **Incomplete Information**: Users may register services with minimal or inaccurate information, reducing catalog value
   - *Mitigation*: Enforce required fields, provide clear guidance on data quality expectations, and enable administrator review to catch issues

2. **Approval Bottleneck**: Service registration approval may become a bottleneck if administrator capacity is insufficient
   - *Mitigation*: Monitor approval queue size and turnaround time; scale administrator team as needed

3. **Abandoned Services**: Services may remain in the catalog after being decommissioned
   - *Mitigation*: Implement periodic review process for service owners to verify service status; DEAD services can indicate decommissioning

4. **Access Control Confusion**: Users may be unclear about who can register or update services
   - *Mitigation*: Provide clear documentation on roles and permissions

5. **Network Instability**: Network issues may cause false DEAD status for healthy services
   - *Mitigation*: Configure reasonable heartbeat timeouts and max missed heartbeats; provide clear guidance on appropriate values for different environments

6. **Heartbeat Storm**: Large number of services sending heartbeats simultaneously may overwhelm the monitoring system
   - *Mitigation*: Implement rate limiting and load balancing; encourage services to stagger heartbeat intervals

7. **Monitoring System Failure**: If monitoring system goes down, all services may be incorrectly marked as DEAD
   - *Mitigation*: Ensure high availability for monitoring service; implement graceful degradation where status remains unchanged during monitoring outages

8. **Incorrect Health Status**: Misconfigured heartbeat parameters (too aggressive or too lenient) may lead to inaccurate health reporting
   - *Mitigation*: Provide recommended heartbeat configurations based on service type; validate parameter ranges during registration

## Open Issues

1. **Authentication & Authorization (P2)**: Detailed authentication and authorization mechanism to be implemented in Priority 2. Current spec assumes existing authentication infrastructure exists. Need to specify:
   - Authentication method (API keys, OAuth2, JWT, mTLS)
   - Authorization model (role-based, attribute-based)
   - Token lifecycle and rotation
   - Service-to-service vs user-to-service authentication patterns
