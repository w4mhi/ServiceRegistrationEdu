# Quickstart Guide: Service Registration API

**Feature**: Service Registration with Heartbeat Monitoring  
**Audience**: Developers setting up local development environment  
**Time**: ~30 minutes

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 8.0 SDK** or later
  - Verify: `dotnet --version` (should show 8.0.x or higher)
  - Download: https://dotnet.microsoft.com/download

- **PostgreSQL 15+** or **SQL Server 2019+**
  - PostgreSQL: https://www.postgresql.org/download/
  - SQL Server: https://www.microsoft.com/sql-server/sql-server-downloads

- **Git** (for cloning repository)
  - Verify: `git --version`

- **Code Editor** (recommended: Visual Studio, VS Code, or Rider)

- **curl** or **Postman** (for testing endpoints)

## Step 1: Clone Repository

```bash
git clone https://github.com/your-org/service-registry.git
cd service-registry
git checkout 1-register-service
```

## Step 2: Database Setup

### Option A: PostgreSQL

1. **Create database**:
   ```bash
   psql -U postgres
   CREATE DATABASE serviceregistry;
   \q
   ```

2. **Update connection string** in `src/Omni.ServiceRegistry.Api/appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "ServiceRegistry": "Host=localhost;Port=5432;Database=serviceregistry;Username=postgres;Password=your_password"
     }
   }
   ```

### Option B: SQL Server

1. **Create database**:
   ```bash
   sqlcmd -S localhost -U sa -P YourPassword
   CREATE DATABASE ServiceRegistry;
   GO
   ```

2. **Update connection string** in `src/Omni.ServiceRegistry.Api/appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "ServiceRegistry": "Server=localhost;Database=ServiceRegistry;User Id=sa;Password=YourPassword;TrustServerCertificate=true"
     }
   }
   ```

## Step 3: Restore Dependencies

From the repository root:

```bash
dotnet restore
```

This will restore all NuGet packages defined in `Directory.Packages.props`.

## Step 4: Apply Database Migrations

Navigate to the API project and run migrations:

```bash
cd src/Omni.ServiceRegistry.Api
dotnet ef database update
```

This creates the database schema (tables, indexes, constraints) based on EF Core migrations.

**Verify tables created**:
```bash
# PostgreSQL
psql -U postgres -d serviceregistry -c "\dt"

# SQL Server
sqlcmd -S localhost -U sa -d ServiceRegistry -Q "SELECT name FROM sys.tables"
```

Expected tables: `RegistrationRequests`, `Services`, `ServiceChangeHistory`

## Step 5: Build Solution

From repository root:

```bash
dotnet build
```

Ensure all projects compile without errors.

## Step 6: Run API

Start the API server:

```bash
cd src/Omni.ServiceRegistry.Api
dotnet run
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

The API is now running at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

## Step 7: Test Endpoints

### 7.1 Register a Service

```bash
curl -X POST http://localhost:5000/api/v1/register \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "payment-service",
    "description": "Handles payment processing and transaction management",
    "owner": "payments-team",
    "contactEmail": "payments@example.com",
    "endpoints": [
      "https://api.payments.example.com",
      "https://api-backup.payments.example.com"
    ],
    "heartbeatTimeout": 30,
    "maxMissedHeartbeats": 10
  }'
```

**Expected Response** (200 OK):
```json
{
  "registrationId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "pending",
  "message": "Registration submitted successfully. Awaiting administrator approval."
}
```

**Save the `registrationId`** for subsequent steps.

### 7.2 Check Registration Status

```bash
curl http://localhost:5000/api/v1/status/registration/550e8400-e29b-41d4-a716-446655440000
```

**Expected Response** (200 OK):
```json
{
  "registrationId": "550e8400-e29b-41d4-a716-446655440000",
  "serviceName": "payment-service",
  "status": "pending",
  "submittedDate": "2025-11-09T14:30:00Z",
  "approvedDate": null,
  "approvedBy": null,
  "deniedDate": null,
  "deniedBy": null,
  "comments": null,
  "serviceId": null
}
```

### 7.3 List Pending Registrations (Admin)

```bash
curl http://localhost:5000/api/v1/admin/registrations/pending
```

**Expected Response** (200 OK):
```json
{
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "registrations": [
    {
      "registrationId": "550e8400-e29b-41d4-a716-446655440000",
      "serviceName": "payment-service",
      "description": "Handles payment processing and transaction management",
      "owner": "payments-team",
      "contactEmail": "payments@example.com",
      "endpoints": [
        "https://api.payments.example.com",
        "https://api-backup.payments.example.com"
      ],
      "heartbeatTimeout": 30,
      "maxMissedHeartbeats": 10,
      "submittedDate": "2025-11-09T14:30:00Z"
    }
  ]
}
```

### 7.4 Approve Registration (Admin)

```bash
curl -X POST http://localhost:5000/api/v1/admin/registrations/550e8400-e29b-41d4-a716-446655440000/approve \
  -H "Content-Type: application/json" \
  -d '{
    "comments": "Service reviewed and approved for production use"
  }'
```

**Expected Response** (200 OK):
```json
{
  "registrationId": "550e8400-e29b-41d4-a716-446655440000",
  "serviceId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "status": "approved",
  "approvedBy": "admin@example.com",
  "approvedDate": "2025-11-09T16:45:00Z",
  "message": "Registration approved. Service is now active in the catalog."
}
```

**Save the `serviceId`** for heartbeat testing.

### 7.5 Check Service Status

```bash
curl http://localhost:5000/api/v1/status/service/7c9e6679-7425-40de-944b-e07fc1f90ae7
```

**Expected Response** (200 OK):
```json
{
  "serviceId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "serviceName": "payment-service",
  "description": "Handles payment processing and transaction management",
  "owner": "payments-team",
  "contactEmail": "payments@example.com",
  "endpoints": [
    "https://api.payments.example.com",
    "https://api-backup.payments.example.com"
  ],
  "healthStatus": "HEALTHY",
  "lastHeartbeatTimestamp": null,
  "lastHeartbeatClientStatus": null,
  "missedHeartbeatCounter": 0,
  "heartbeatTimeout": 30,
  "maxMissedHeartbeats": 10,
  "registrationDate": "2025-11-09T16:45:00Z",
  "lastUpdatedDate": "2025-11-09T16:45:00Z"
}
```

### 7.6 Send Heartbeat

```bash
curl -X POST http://localhost:5000/api/v1/heartbeat/7c9e6679-7425-40de-944b-e07fc1f90ae7 \
  -H "Content-Type: application/json" \
  -d '{
    "clientStatus": "running"
  }'
```

**Expected Response** (200 OK):
```json
{
  "acknowledged": true,
  "timestamp": "2025-11-09T14:55:30Z",
  "message": "Heartbeat received"
}
```

### 7.7 Request Service Deletion

```bash
curl -X POST http://localhost:5000/api/v1/delete/7c9e6679-7425-40de-944b-e07fc1f90ae7 \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Service decommissioned for testing purposes"
  }'
```

**Expected Response** (200 OK):
```json
{
  "serviceId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "deletionStatus": "pending",
  "requestedBy": "owner@example.com",
  "requestedDate": "2025-11-09T17:00:00Z",
  "approvedBy": null,
  "approvedDate": null,
  "message": "Deletion request submitted. Awaiting administrator approval."
}
```

## Step 8: Run Tests

### Unit Tests

```bash
cd tests/Omni.ServiceRegistry.Services.Tests
dotnet test
```

### Integration Tests

```bash
cd tests/Omni.ServiceRegistry.Api.Tests
dotnet test
```

### All Tests

From repository root:
```bash
dotnet test
```

Expected output: All tests passing (green)

## Step 9: View OpenAPI Documentation

With the API running, navigate to:

**Swagger UI**: http://localhost:5000/swagger

This provides:
- Interactive API documentation
- Schema definitions
- Try-it-out functionality for each endpoint

## Common Issues & Troubleshooting

### Issue: Database connection fails

**Error**: `Microsoft.Data.SqlClient.SqlException: A network-related or instance-specific error occurred`

**Solution**:
1. Verify database server is running
2. Check connection string credentials
3. For SQL Server, ensure TCP/IP is enabled in SQL Server Configuration Manager
4. For PostgreSQL, check `pg_hba.conf` allows connections from localhost

### Issue: Migration fails with "relation already exists"

**Error**: `Npgsql.PostgresException: 42P07: relation "Services" already exists`

**Solution**:
```bash
# Drop database and recreate
dotnet ef database drop --force
dotnet ef database update
```

### Issue: Port already in use

**Error**: `System.IO.IOException: Failed to bind to address http://127.0.0.1:5000: address already in use`

**Solution**:
```bash
# Find process using port 5000
lsof -ti:5000 | xargs kill -9  # macOS/Linux
netstat -ano | findstr :5000   # Windows (then taskkill /PID <pid> /F)

# Or change port in appsettings.json
{
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://localhost:5050" }
    }
  }
}
```

### Issue: Heartbeat monitor not detecting timeouts

**Symptom**: Services remain HEALTHY despite missing heartbeats

**Solution**:
1. Check `HeartbeatMonitorService` is registered in `Program.cs`:
   ```csharp
   builder.Services.AddHostedService<HeartbeatMonitorService>();
   ```
2. Verify background service is running (check logs for "Heartbeat monitor service starting")
3. Check polling interval (default 5 seconds) in configuration

### Issue: Validation errors on registration

**Error**: `400 Bad Request: Invalid service name format`

**Solution**: Ensure service name matches pattern:
- Alphanumeric with hyphens only
- 3-50 characters
- Example valid names: `payment-service`, `auth-api-v2`, `user-mgmt`
- Invalid: `Payment Service` (spaces), `api_service` (underscores), `ab` (too short)

## Configuration

### appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Omni.ServiceRegistry": "Debug"
    }
  },
  "ConnectionStrings": {
    "ServiceRegistry": "Host=localhost;Port=5432;Database=serviceregistry;Username=postgres;Password=your_password"
  },
  "HeartbeatMonitoring": {
    "CheckIntervalSeconds": 5,
    "EnableMonitoring": true
  },
  "RateLimiting": {
    "Heartbeat": {
      "PermitLimit": 200,
      "WindowSeconds": 1
    },
    "Registration": {
      "PermitLimit": 10,
      "WindowSeconds": 60
    }
  }
}
```

### Environment Variables

Alternatively, set via environment variables:

```bash
export ConnectionStrings__ServiceRegistry="Host=localhost;Database=serviceregistry;Username=postgres;Password=your_password"
export HeartbeatMonitoring__CheckIntervalSeconds="5"
export ASPNETCORE_ENVIRONMENT="Development"
```

## Development Workflow

1. **Make code changes** in your editor
2. **Rebuild**: `dotnet build`
3. **Run tests**: `dotnet test`
4. **Restart API**: Stop with Ctrl+C, then `dotnet run`
5. **Test endpoints** with curl or Postman
6. **Check logs** in console output (structured logging with ILogger)

## Next Steps

- **Authentication**: Implement P2 authentication/authorization (currently deferred)
- **Service Catalog UI**: Build dashboard for browsing registered services
- **Metrics**: Add Prometheus/Grafana for heartbeat monitoring visualization
- **Alerts**: Configure notifications for DEAD/DEGRADED services
- **Load Testing**: Test 10,000 heartbeats/minute throughput target
- **Docker**: Containerize API for deployment (`Dockerfile` in API project)

## Additional Resources

- **Feature Specification**: `specs/1-register-service/spec.md`
- **Implementation Plan**: `specs/1-register-service/plan.md`
- **Data Model**: `specs/1-register-service/data-model.md`
- **API Contracts**: `specs/1-register-service/contracts/*.yaml`
- **.NET Documentation**: https://learn.microsoft.com/dotnet/
- **EF Core Documentation**: https://learn.microsoft.com/ef/core/

## Support

For issues or questions:
- Check GitHub Issues: https://github.com/your-org/service-registry/issues
- Team Slack: #platform-services
- Email: platform@example.com

---

**Quickstart Complete!** You now have a working local development environment for the Service Registration API. Happy coding! 🚀
