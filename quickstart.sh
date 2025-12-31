#!/bin/bash
# Service Registry MVP Quickstart
# This script starts the Service Registry API and Dashboard with PostgreSQL

set -e

echo "🚀 Service Registry MVP Quickstart"
echo "=================================="
echo ""

# Check prerequisites
command -v dotnet >/dev/null 2>&1 || { echo "❌ .NET 9.0 SDK required but not installed. Aborting." >&2; exit 1; }
command -v docker >/dev/null 2>&1 || { echo "❌ Docker required but not installed. Aborting." >&2; exit 1; }

# Stop any running .NET services
echo "🧹 Stopping any running services..."
pkill -f "Omni.ServiceRegistry.Api" 2>/dev/null && echo "  ✓ API stopped" || echo "  ℹ️  No API running"
pkill -f "Omni.ServiceRegistry.Dashboard" 2>/dev/null && echo "  ✓ Dashboard stopped" || echo "  ℹ️  No Dashboard running"

# Also kill by port to ensure ports are freed
lsof -ti:5159 2>/dev/null | xargs kill -9 2>/dev/null && echo "  ✓ Port 5159 freed" || true
lsof -ti:5083 2>/dev/null | xargs kill -9 2>/dev/null && echo "  ✓ Port 5083 freed" || true

# Wait for processes to release connections
sleep 2

# Clean up existing PostgreSQL container and data
echo "🧹 Cleaning up old PostgreSQL data..."
docker stop serviceregistry-postgres 2>/dev/null && echo "  ✓ Container stopped" || echo "  ℹ️  Container not running"
docker rm serviceregistry-postgres 2>/dev/null && echo "  ✓ Container removed" || echo "  ℹ️  Container not found"

# Start fresh PostgreSQL
echo "📦 Starting PostgreSQL with clean database..."
docker run -d \
  --name serviceregistry-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=serviceregistry \
  -p 5432:5432 \
  postgres:16-alpine

# Wait for PostgreSQL to be ready
echo "⏳ Waiting for PostgreSQL to be ready..."
for i in {1..30}; do
    if docker exec serviceregistry-postgres pg_isready -U postgres >/dev/null 2>&1; then
        echo "✓ PostgreSQL is ready"
        # Give it more time to fully initialize and accept connections
        sleep 5
        break
    fi
    sleep 1
    if [ $i -eq 30 ]; then
        echo "✗ PostgreSQL failed to start"
        exit 1
    fi
done

# Build solution
echo "🔨 Building solution..."
dotnet build --configuration Release --nologo --verbosity quiet

# Run migrations
echo "🗄️  Applying database migrations..."
cd Omni.ServiceRegistry.Data
dotnet ef database update --startup-project ../Omni.ServiceRegistry.Api --no-build || {
    echo "✗ Migration failed. Retrying in 3 seconds..."
    sleep 3
    dotnet ef database update --startup-project ../Omni.ServiceRegistry.Api --no-build || {
        echo "✗ Migration failed again. Exiting..."
        exit 1
    }
}
cd ..

echo ""
echo "✅ Setup complete!"
echo ""
echo "Starting services..."
echo "  - API will run on http://localhost:5159"
echo "  - Dashboard will run on http://localhost:5083"
echo "  - API Docs: http://localhost:5159/scalar/v1"
echo ""
echo "Press Ctrl+C to stop all services"
echo ""

# Start API in background
cd Omni.ServiceRegistry.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --configuration Release --no-build &
API_PID=$!
cd ..

# Wait for API to start
sleep 5

# Start Dashboard in background
cd Omni.ServiceRegistry.Dashboard
ASPNETCORE_ENVIRONMENT=Development dotnet run --configuration Release --no-build &
DASHBOARD_PID=$!
cd ..

# Wait for user interrupt
trap "echo ''; echo '🛑 Stopping services...'; kill $API_PID $DASHBOARD_PID 2>/dev/null; exit" INT

echo "✅ Services started!"
echo "   API PID: $API_PID"
echo "   Dashboard PID: $DASHBOARD_PID"
echo ""

# Keep script running
wait
