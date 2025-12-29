#!/bin/bash

echo "======================================"
echo "Restarting Service Registry Servers"
echo "======================================"
echo ""

# Kill any running instances
echo "Stopping any running instances..."
pkill -f "dotnet.*Omni.ServiceRegistry.Api" 2>/dev/null
pkill -f "dotnet.*Omni.ServiceRegistry.Dashboard" 2>/dev/null
sleep 2

# Check PostgreSQL
echo ""
echo "Checking PostgreSQL..."
if docker ps | grep -q serviceregistry-postgres; then
    echo "✓ PostgreSQL is running"
else
    echo "✗ PostgreSQL not running. Starting..."
    docker run -d \
      --name serviceregistry-postgres \
      -e POSTGRES_PASSWORD=postgres \
      -e POSTGRES_DB=serviceregistry \
      -p 5432:5432 \
      postgres:16-alpine
    sleep 3
fi

# Apply migrations
echo ""
echo "Applying database migrations..."
cd Omni.ServiceRegistry.Api
dotnet ef database update --project ../Omni.ServiceRegistry.Data --no-build
cd ..

# Start API
echo ""
echo "Starting API..."
cd Omni.ServiceRegistry.Api
nohup dotnet run > ../logs/api.log 2>&1 &
API_PID=$!
echo "API started (PID: $API_PID)"
cd ..

# Wait for API to be ready
echo "Waiting for API to be ready..."
for i in {1..30}; do
    if curl -s http://localhost:5159/health > /dev/null 2>&1; then
        echo "✓ API is ready"
        break
    fi
    sleep 1
    if [ $i -eq 30 ]; then
        echo "✗ API failed to start"
        exit 1
    fi
done

# Start Dashboard
echo ""
echo "Starting Dashboard..."
cd Omni.ServiceRegistry.Dashboard
nohup dotnet run > ../logs/dashboard.log 2>&1 &
DASHBOARD_PID=$!
echo "Dashboard started (PID: $DASHBOARD_PID)"
cd ..

# Wait for Dashboard to be ready
echo "Waiting for Dashboard to be ready..."
for i in {1..30}; do
    if curl -s http://localhost:5083/health > /dev/null 2>&1; then
        echo "✓ Dashboard is ready"
        break
    fi
    sleep 1
    if [ $i -eq 30 ]; then
        echo "✗ Dashboard failed to start"
        exit 1
    fi
done

echo ""
echo "======================================"
echo "✓ All services started successfully!"
echo "======================================"
echo ""
echo "API:       http://localhost:5159"
echo "Dashboard: http://localhost:5083"
echo "API Docs:  http://localhost:5159/scalar/v1"
echo ""
echo "Logs:"
echo "  API:       tail -f logs/api.log"
echo "  Dashboard: tail -f logs/dashboard.log"
echo ""
