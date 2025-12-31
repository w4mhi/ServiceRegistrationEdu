# AI-Powered Health Insights - Implementation Complete

## Overview

The AI-powered health insights system has been fully implemented across all four phases. This document summarizes the complete implementation and provides testing guidance.

## Implementation Summary

### Phase 1: Foundation & Data Layer ✅
**Status**: Complete

**Database Schema**:
- `ServiceHealthInsights` table with JSONB columns for structured AI output
- `AnalysisTriggerLogs` table for tracking trigger sources
- Indexes on service_id, generated_at, trigger_type
- Constraints ensuring data integrity

**Migrations**:
- Created initial schema migration
- Applied to database successfully

### Phase 2: LLM Integration ✅
**Status**: Complete

**Services Implemented**:
1. **OllamaService** (`Omni.ServiceRegistry.Services/LLM/OllamaService.cs`)
   - HTTP client integration with Ollama API
   - phi4:latest model (14.7B parameters, Q4_K_M quantization)
   - 4500 max tokens, 0.7 temperature
   - Availability check endpoint

2. **ContextGathererService** (`Omni.ServiceRegistry.Services/LLM/ContextGathererService.cs`)
   - 5-layer context gathering:
     - Service identity (name, ID, health status)
     - Health timeline (last 50 transitions)
     - Historical patterns (averages, trends)
     - Correlated services (15-minute window)
     - Recent changes (audit log)
   - ~4500 token budget allocation

3. **PromptTemplateService** (`Omni.ServiceRegistry.Services/LLM/PromptTemplateService.cs`)
   - System prompt defining AI analyst role
   - Structured user prompt with context injection
   - Instructions for JSON format output
   - 5 required sections: summary, root causes, correlated services, historical context, recommended actions

4. **HealthInsightsAnalysisService** (`Omni.ServiceRegistry.Services/LLM/HealthInsightsAnalysisService.cs`)
   - Orchestrates end-to-end analysis workflow
   - Context gathering → Prompt building → LLM inference → JSON parsing → Database storage
   - Robust error handling and logging
   - Token usage and processing time tracking

5. **AnalysisQueueService** (`Omni.ServiceRegistry.Services/LLM/AnalysisQueueService.cs`)
   - Priority-based queue (0=scheduled, 1=manual, 2=automatic)
   - Deduplication by service ID
   - Cooldown enforcement (60 seconds)
   - Thread-safe concurrent operations

**Noise Reduction**:
- Filters out HEALTHY services from batch analysis
- Focuses on UNHEALTHY, DEGRADED, DEAD statuses
- Prevents redundant analysis via cooldown

### Phase 3: API & Background Services ✅
**Status**: Complete

**REST API** (`Omni.ServiceRegistry.Api/Controllers/HealthInsightsController.cs`):
- `POST /api/v1/insights/analyze` - Trigger analysis (global or per-service)
- `GET /api/v1/insights/service/{id}/latest` - Get latest insight for service
- `GET /api/v1/insights/service/{id}` - Get all insights with optional `since` filter
- `GET /api/v1/insights/recent` - Get recent insights (max 100, default 20)
- `GET /api/v1/insights/{insightId}` - Get specific insight by ID

**Rate Limiting**:
- Server-side: 5 requests per 5 minutes (sliding window)
- Client-side: 60-second cooldown enforced in queue
- Rate limit configuration in appsettings.json

**Background Workers**:
1. **HealthInsightsWorker** (`Omni.ServiceRegistry.Api/BackgroundServices/HealthInsightsWorker.cs`)
   - Processes analysis queue in background
   - 5-second polling interval
   - SignalR notifications for all analysis events

2. **BatchAnalysisScheduler** (`Omni.ServiceRegistry.Api/BackgroundServices/BatchAnalysisScheduler.cs`)
   - Runs daily at midnight (configurable)
   - Analyzes all unhealthy services
   - Priority 0 for scheduled analysis

**HeartbeatMonitorService Integration**:
- Automatic analysis trigger on critical transitions:
  - HEALTHY → DEAD (service failure)
  - HEALTHY → DEGRADED (service degrading)
- Priority 2 for automatic triggers
- Enables proactive health insights

**SignalR Notifications**:
- `AnalysisStarted` (serviceId, startedAt)
- `AnalysisCompleted` (serviceId, insightId, completedAt)
- `AnalysisSkipped` (serviceId, reason, skippedAt)
- `AnalysisFailed` (serviceId, error, failedAt)

### Phase 4: Dashboard UI ✅
**Status**: Complete

**Services**:
1. **CooldownTimerService** (`Omni.ServiceRegistry.Dashboard/Services/CooldownTimerService.cs`)
   - 60-second countdown timer
   - Event-driven UI updates
   - Thread-safe operations
   - Properties: `IsOnCooldown`, `RemainingSeconds`
   - Events: `OnCooldownChanged`

2. **HealthInsightsApiClient** (`Omni.ServiceRegistry.Dashboard/Services/HealthInsightsApiClient.cs`)
   - HTTP client wrapper for insights API
   - Methods for all 5 API endpoints
   - Error handling with logging
   - Returns null on 404, throws on other errors

**UI Components**:

1. **Health Insights Page** (`Pages/HealthInsights.razor`)
   - Stats dashboard (4 cards):
     - Total analyses
     - Completed count
     - Failed count
     - Average tokens used
   - Insight cards with color-coded status:
     - ✅ Green border: Completed
     - ❌ Red border: Failed
     - ⏳ Orange border: Processing/Queued
   - Sections per insight:
     - Summary
     - Root causes (bullet list)
     - Correlated services (table with impact)
     - Historical context
     - Recommended actions (bullet list)
   - Trigger type badges (Manual, Automatic, Scheduled)
   - Status badges
   - Processing metadata (tokens, time, model)

2. **Monitoring Dashboard Enhancements** (`Pages/Index.razor`)
   - **Global AI Analysis Button** (header):
     - Location: Page header next to refresh button
     - Displays cooldown countdown: "🧠 AI Analysis (Xs)"
     - Disabled when analyzing or on cooldown
     - Triggers global analysis for all unhealthy services
   
   - **Per-Service AI Button** (service table):
     - Location: Actions column, before Details button
     - Icon: 🤖
     - Same cooldown/analyzing state as global button
     - Triggers analysis for specific service
   
   - **Insight Badges** (service name):
     - Shows "⚠️ New AI insights" for services with insights < 24h old
     - Clickable, navigates to /insights page
     - Tooltip shows relative time (e.g., "2h ago")
     - Located below restoration badges
   
   - **SignalR Integration**:
     - `OnAnalysisStarted`: Sets `isAnalyzing = true`, updates UI
     - `OnAnalysisCompleted`: Refreshes insights, sets `isAnalyzing = false`
     - `OnAnalysisFailed`: Sets `isAnalyzing = false`
     - Real-time updates without page refresh

3. **NavMenu** (`Components/Layout/NavMenu.razor`)
   - Added "Health Insights" link with 🧠 icon
   - Route: /insights

**CSS Styling** (`wwwroot/css/modern-dashboard.css`):
- `.insight-badge`: Yellow background, inline badge
- `.insight-card`: White card with hover transform
- `.insight-completed`: Green left border
- `.insight-failed`: Red left border
- `.insight-processing`: Orange left border
- `.btn-ai-analysis`: Purple gradient, hover animation
- `.btn-ai-analysis-small`: Compact variant for table actions

## Configuration

### appsettings.json (API)
```json
{
  "LlmSettings": {
    "OllamaBaseUrl": "http://localhost:11434",
    "ModelName": "phi4:latest",
    "MaxTokens": 4500,
    "Temperature": 0.7,
    "TimeoutSeconds": 120
  },
  "HealthInsights": {
    "AnalysisCooldownSeconds": 60,
    "BatchScheduleCron": "0 0 * * *",
    "MaxHistoryEvents": 50,
    "CorrelationWindowMinutes": 15
  },
  "RateLimiting": {
    "HealthInsights": {
      "PermitLimit": 5,
      "Window": "00:05:00",
      "QueueLimit": 10
    }
  }
}
```

## Testing Guide

### Prerequisites
1. **Ollama**: Running with phi4:latest model
   ```bash
   ollama pull phi4:latest
   ollama serve
   ```

2. **PostgreSQL**: Database with migrations applied
   ```bash
   cd deployment/docker
   docker-compose up -d postgres
   ```

3. **Services**: API and Dashboard running
   ```bash
   ./quickstart.sh
   ```

### Test Scenarios

#### Scenario 1: Manual Global Analysis
1. Navigate to dashboard (http://localhost:5241)
2. Ensure at least one service is UNHEALTHY/DEGRADED/DEAD
3. Click "🧠 AI Analysis" button in header
4. Observe:
   - Button shows cooldown: "🧠 AI Analysis (60s)"
   - Button becomes disabled
   - Countdown decreases every second
5. Navigate to "Health Insights" page
6. Verify insight appears with:
   - Trigger type: "Manual"
   - Status: "Completed" (green border)
   - All 5 sections populated

#### Scenario 2: Per-Service Analysis
1. Navigate to dashboard
2. Find UNHEALTHY service in table
3. Click 🤖 button in Actions column
4. Observe cooldown (shared with global button)
5. Check "Health Insights" page for service-specific insight

#### Scenario 3: Automatic Analysis Trigger
1. Run client simulator to create healthy services:
   ```bash
   ./run-client-simulator.sh
   ```
2. Wait for service to transition HEALTHY → DEAD (stop sending heartbeats)
3. Observe automatic analysis triggered (Priority 2)
4. Check insight on dashboard and insights page
5. Verify trigger type: "Automatic"

#### Scenario 4: Batch Scheduled Analysis
1. Modify `BatchAnalysisScheduler` to run every minute (for testing):
   ```csharp
   TimeSpan delay = TimeSpan.FromMinutes(1);
   ```
2. Ensure multiple UNHEALTHY services exist
3. Wait for scheduled time
4. Verify insights generated with trigger type: "Scheduled"

#### Scenario 5: Real-Time Updates
1. Open dashboard in two browser windows side-by-side
2. In window 1: Click "🧠 AI Analysis"
3. In window 2: Observe:
   - "Analyzing..." state appears
   - Insight badge appears when complete (if < 24h)
   - No page refresh needed

#### Scenario 6: Insight Badge Display
1. Trigger analysis for service (manual or automatic)
2. Wait for completion
3. Navigate to dashboard
4. Verify "⚠️ New AI insights" badge appears below service name
5. Click badge → navigates to /insights page
6. Verify relative time in tooltip (e.g., "5m ago")

#### Scenario 7: Cooldown Enforcement
1. Click "🧠 AI Analysis" button
2. Attempt to click again before 60s elapsed
3. Verify button remains disabled
4. Wait for countdown to reach 0
5. Verify button re-enables
6. Click again successfully

#### Scenario 8: Rate Limiting
1. Use API client to trigger 6 analysis requests within 5 minutes
2. Verify 6th request returns 429 Too Many Requests
3. Wait 5 minutes
4. Verify requests allowed again

### Validation Checklist

**Database**:
- [ ] `ServiceHealthInsights` table populated
- [ ] JSONB columns contain structured data
- [ ] `AnalysisTriggerLogs` records all triggers
- [ ] Indexes improve query performance

**API**:
- [ ] All 5 endpoints return expected data
- [ ] Rate limiting enforces 5 per 5 minutes
- [ ] Cooldown prevents duplicate analysis
- [ ] SignalR broadcasts all 4 event types

**LLM**:
- [ ] Ollama generates insights successfully
- [ ] Context includes all 5 layers
- [ ] Output follows JSON schema
- [ ] Token usage logged correctly

**Background Services**:
- [ ] HealthInsightsWorker processes queue
- [ ] BatchAnalysisScheduler runs at midnight
- [ ] HeartbeatMonitorService triggers automatic analysis

**Dashboard**:
- [ ] Global AI button works with cooldown
- [ ] Per-service AI button works
- [ ] Insight badges appear for recent insights
- [ ] Health Insights page displays all insights
- [ ] SignalR updates UI in real-time
- [ ] Cooldown countdown updates every second

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         Dashboard UI                            │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐   │
│  │ Index.razor  │  │HealthInsights│  │ CooldownTimerSvc   │   │
│  │ (Monitoring) │  │   Page       │  │ HealthInsightsAPI  │   │
│  └──────┬───────┘  └──────┬───────┘  └──────────┬─────────┘   │
│         │                 │                      │              │
│         └─────────────────┴──────────────────────┘              │
│                           │                                     │
│                      SignalR (Real-time)                        │
└───────────────────────────┼─────────────────────────────────────┘
                            │
┌───────────────────────────┼─────────────────────────────────────┐
│                      API Layer                                  │
│  ┌──────────────────┐    │    ┌─────────────────────────┐      │
│  │ HealthInsights   │◄───┴───►│ SignalRHealthStatus     │      │
│  │   Controller     │          │      Notifier           │      │
│  └────────┬─────────┘          └─────────────────────────┘      │
│           │                                                      │
│  ┌────────▼─────────┐    ┌──────────────────────┐              │
│  │HealthInsights    │    │  Batch Analysis      │              │
│  │    Worker        │    │    Scheduler         │              │
│  └────────┬─────────┘    └──────────┬───────────┘              │
│           │                          │                          │
│           └──────────┬───────────────┘                          │
│                      │                                          │
└──────────────────────┼──────────────────────────────────────────┘
                       │
┌──────────────────────┼──────────────────────────────────────────┐
│                 Services Layer                                  │
│  ┌───────────────────▼─────────────┐                           │
│  │  AnalysisQueueService           │                           │
│  │  (Priority Queue, Cooldown)     │                           │
│  └───────────────┬─────────────────┘                           │
│                  │                                              │
│  ┌───────────────▼──────────────────────────────────────┐      │
│  │  HealthInsightsAnalysisService                       │      │
│  │  (Orchestrator)                                      │      │
│  └─┬──────────┬──────────┬──────────┬──────────────────┘      │
│    │          │          │          │                          │
│  ┌─▼────────┐ ┌▼────────┐ ┌▼───────┐ ┌▼─────────────┐         │
│  │ Context  │ │ Prompt  │ │ Ollama │ │ DB Storage   │         │
│  │ Gatherer │ │Template │ │Service │ │ (JSONB)      │         │
│  └──────────┘ └─────────┘ └────────┘ └──────────────┘         │
│                                │                                │
└────────────────────────────────┼────────────────────────────────┘
                                 │
                        ┌────────▼──────────┐
                        │  Ollama Server    │
                        │  phi4:latest      │
                        │  (14.7B params)   │
                        └───────────────────┘
```

## Performance Metrics

**Expected Performance**:
- Context gathering: ~200ms per service
- LLM inference: 5-15 seconds (phi4:latest, Q4_K_M)
- JSON parsing: <50ms
- Database write: ~100ms
- **Total analysis time**: 6-20 seconds per service

**Queue Processing**:
- Polling interval: 5 seconds
- Concurrent analyses: 1 (sequential processing)
- Queue capacity: Unlimited (in-memory ConcurrentQueue)

**Rate Limits**:
- API: 5 requests per 5 minutes
- Cooldown: 60 seconds between analyses

## Known Limitations

1. **Sequential Processing**: Queue processes one analysis at a time
   - Future: Add concurrent processing with configurable workers

2. **In-Memory Queue**: Queue lost on application restart
   - Future: Persist queue to Redis or database

3. **Fixed Context Budget**: 4500 tokens split across 5 layers
   - Future: Dynamic budget allocation based on data availability

4. **No Auth**: API endpoints lack authentication
   - Future: Add JWT bearer token auth (Priority 2)

5. **24-Hour Badge Expiry**: Insight badges disappear after 24h
   - Future: Configurable expiry duration

## Troubleshooting

### Issue: Ollama not responding
**Solution**:
```bash
ollama serve
curl http://localhost:11434/api/tags
```

### Issue: Analysis stuck in queue
**Solution**:
- Check logs: `Dequeueing analysis request for service {serviceId}`
- Verify HealthInsightsWorker is running
- Check LLM timeout (default 120s)

### Issue: SignalR updates not working
**Solution**:
- Check browser console for connection errors
- Verify SignalR hub started: `SignalR hub started successfully`
- Restart API and Dashboard

### Issue: Cooldown not enforcing
**Solution**:
- Check `AnalysisTriggerLogs` table for last trigger timestamp
- Verify `HealthInsights:AnalysisCooldownSeconds` in appsettings.json
- Clear browser cache

### Issue: Rate limit 429 errors
**Solution**:
- Wait 5 minutes for window to reset
- Adjust `RateLimiting:HealthInsights:PermitLimit` in appsettings.json
- Use per-service analysis instead of global

## Next Steps (Priority 2)

1. **Authentication**: Add JWT bearer token auth to API endpoints
2. **Concurrent Processing**: Multiple analysis workers
3. **Redis Queue**: Persistent queue with distributed locking
4. **WebSocket Compression**: Reduce SignalR bandwidth
5. **Insight History Pagination**: Paginate insights on UI
6. **Export Functionality**: Export insights to PDF/JSON
7. **Email Notifications**: Email admins when critical insights generated
8. **Slack Integration**: Post insights to Slack channel
9. **Advanced Analytics**: Trends, patterns, anomaly detection
10. **Multi-Model Support**: Add GPT-4, Claude, Gemini options

## Conclusion

The AI-powered health insights system is **fully operational** and ready for simulation testing. All four phases are complete:
- ✅ Phase 1: Database schema
- ✅ Phase 2: LLM integration
- ✅ Phase 3: API & background services
- ✅ Phase 4: Dashboard UI

Run `./quickstart.sh` and `./run-client-simulator.sh` to test the complete workflow.
