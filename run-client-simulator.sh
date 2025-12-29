#!/bin/bash

# Service Registry Client Simulator - Quick Start Script
# This script helps test the Service Registry by running simulated services

set -e

echo "==================================="
echo "Service Registry Client Simulator"
echo "==================================="
echo ""

# Configuration
API_URL="${API_BASE_URL:-http://localhost:5159}"
DASHBOARD_URL="${DASHBOARD_BASE_URL:-http://localhost:5083}"
API_DIR="Omni.ServiceRegistry.Api"
DASH_DIR="Omni.ServiceRegistry.Dashboard"
CLIENT_DIR="Omni.ServiceRegistry.Client"

# Function to check if a process is running by name
is_process_running() {
    pgrep -f "$1" > /dev/null 2>&1
}

# Check if API is running
echo "Checking if Service Registry API is running at $API_URL..."
if ! curl -s -f "$API_URL/health" > /dev/null 2>&1; then
    echo "⚠️  API is not running. Starting API..."
    echo ""
    
    # Start API in background
    cd "$API_DIR"
    dotnet run > /dev/null 2>&1 &
    API_PID=$!
    cd - > /dev/null
    
    # Wait for API to be ready (max 30 seconds)
    echo "Waiting for API to start..."
    for i in {1..30}; do
        if curl -s -f "$API_URL/health" > /dev/null 2>&1; then
            echo "✓ API is now running (PID: $API_PID)"
            break
        fi
        if [ $i -eq 30 ]; then
            echo "❌ ERROR: API failed to start within 30 seconds"
            kill $API_PID 2>/dev/null || true
            exit 1
        fi
        sleep 1
    done
else
    echo "✓ API is already running"
fi
echo ""

# Check if Dashboard is running
echo "Checking if Dashboard is running at $DASHBOARD_URL..."
if ! curl -s -f "$DASHBOARD_URL" > /dev/null 2>&1; then
    echo "⚠️  Dashboard is not running. Starting Dashboard..."
    echo ""
    
    # Start Dashboard in background
    cd "$DASH_DIR"
    dotnet run > /dev/null 2>&1 &
    DASH_PID=$!
    cd - > /dev/null
    
    # Wait for Dashboard to be ready (max 30 seconds)
    echo "Waiting for Dashboard to start..."
    for i in {1..30}; do
        if curl -s -f "$DASHBOARD_URL" > /dev/null 2>&1; then
            echo "✓ Dashboard is now running (PID: $DASH_PID)"
            break
        fi
        if [ $i -eq 30 ]; then
            echo "❌ ERROR: Dashboard failed to start within 30 seconds"
            kill $DASH_PID 2>/dev/null || true
            exit 1
        fi
        sleep 1
    done
else
    echo "✓ Dashboard is already running"
fi
echo ""

# Check if project exists
if [ ! -f "$CLIENT_DIR/Omni.ServiceRegistry.Client.csproj" ]; then
    echo "❌ ERROR: Client project not found at $CLIENT_DIR"
    exit 1
fi

# Build the client
echo "Building client..."
dotnet build "$CLIENT_DIR" --nologo -v quiet
if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi
echo "✓ Build successful"
echo ""

# Run the simulator
echo "Starting simulator with 5 services..."
echo ""
echo "Services to be simulated:"
echo "  1. payment-service     - AlwaysHealthy (never misses)"
echo "  2. inventory-service   - OccasionalMisses (misses every 5th)"
echo "  3. notification-service - FrequentMisses (misses every 3rd)"
echo "  4. analytics-service   - DeadThenRecover (full cycle)"
echo "  5. reporting-service   - Chaotic (random 70% success)"
echo ""
echo "Press Ctrl+C to stop all services"
echo "-----------------------------------"
echo ""

cd "$CLIENT_DIR"
API_BASE_URL="$API_URL" dotnet run --no-build
