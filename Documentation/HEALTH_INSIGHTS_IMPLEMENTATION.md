
# Health Status Anomaly Explanation - Implementation Plan

**Feature**: AI-powered health insights using Ollama + phi4 local LLM  
**Priority**: P1 (Top 3 LLM Integration Features)  
**Target**: 4-week implementation (Phases 1-4)  
**Status**: 📋 Planning Complete - Ready for Implementation

---

## 🎯 Feature Overview

### What We're Building
An intelligent health analysis system that provides human-readable incident summaries, root cause analysis, correlation detection, and actionable recommendations when services experience health issues.

### Key Decisions
- **LLM Provider**: Ollama with phi4 (local, customizable)
- **UI Placement**: New "Health Insights" tab + per-service AI buttons
- **Analysis Types**: Real-time (critical events) + Batch (daily patterns) + On-Demand (admin-triggered)
- **Context Window**: 5-layer context (service config, health timeline, historical patterns, correlations, recent changes)
- **Rate Limiting**: 60-second client-side cooldown + server-side rate limiting (5 per 5 min)

---

## 📊 Implementation Phases

### Phase 1: Foundation & Data Layer (Week 1)
**Goal**: Set up database schema, models, and configuration

### Phase 2: LLM Integration & Analysis Engine (Week 2)
**Goal**: Integrate Ollama, build context gathering, implement analysis logic

### Phase 3: API & Background Services (Week 2-3)
**Goal**: Create API endpoints, background workers, and queue processing

### Phase 4: Dashboard UI & UX (Week 3-4)
**Goal**: Build UI components, buttons, Health Insights tab, real-time updates

---

## 📋 Task Breakdown with Priorities

### PHASE 1: Foundation & Data Layer

#### P0 - Critical (Must Complete First)

- [x] **T1.1** - Create `ServiceHealthInsight` Model
  - **Location**: `Omni.ServiceRegistry.Models/ServiceHealthInsight.cs`
  - **Dependencies**: None
  - **Estimated Time**: 1 hour
  - **Status**: ✅ COMPLETE
  - **Details**:
    - Properties: InsightId, ServiceId, GeneratedAt, TriggerType, TriggeredBy
    - LLM results: Summary, RootCauses (JSON), CorrelatedServices (JSON), HistoricalContext, RecommendedActions (JSON)
    - Metadata: LlmModel, TokensUsed, ProcessingTimeMs, AnalysisStatus, ErrorMessage
    - Context snapshot: ContextData (JSON for audit)
    - Navigation: Service property
  - **Acceptance Criteria**:
    - Model compiles without errors ✅
    - All properties have appropriate data types ✅
    - Navigation properties configured ✅

- [x] **T1.2** - Create `AnalysisTriggerLog` Model
  - **Location**: `Omni.ServiceRegistry.Models/AnalysisTriggerLog.cs`
  - **Dependencies**: None
  - **Estimated Time**: 30 minutes
  - **Status**: ✅ COMPLETE
  - **Details**:
    - Track admin trigger timestamps for cooldown enforcement
    - Properties: LogId, TriggeredBy, TriggeredAt, RequestType (Global/PerService), ServiceIds
  - **Acceptance Criteria**:
    - Model compiles without errors ✅
    - Proper indexing on TriggeredBy + TriggeredAt ✅

- [x] **T1.3** - Add EF Core DbContext Configuration
  - **Location**: `Omni.ServiceRegistry.Data/Postgres/ServiceRegistryDbContext.cs`
  - **Dependencies**: T1.1, T1.2
  - **Estimated Time**: 1 hour
  - **Status**: ✅ COMPLETE
  - **Details**:
    - Add `DbSet<ServiceHealthInsight>` and `DbSet<AnalysisTriggerLog>` ✅
    - Configure entity mappings in `OnModelCreating` ✅
    - Indexes: ServiceId, GeneratedAt, AnalysisStatus, TriggeredBy+TriggeredAt ✅
    - String max lengths: Summary (2000), RootCauses (5000), CorrelatedServices (3000), etc. ✅
  - **Acceptance Criteria**:
    - EF configuration compiles ✅
    - All relationships defined ✅
    - Appropriate indexes created ✅

- [x] **T1.4** - Create EF Core Migration
  - **Location**: `Omni.ServiceRegistry.Data/Postgres/Migrations/`
  - **Dependencies**: T1.3
  - **Estimated Time**: 30 minutes
  - **Status**: ✅ COMPLETE
  - **Command**: `dotnet ef migrations add AddHealthInsightsTables -p Omni.ServiceRegistry.Data`
  - **Acceptance Criteria**:
    - Migration file generated successfully ✅
    - Up() and Down() methods create/drop tables correctly ✅
    - No build errors ✅

- [x] **T1.5** - Apply Migration to Database
  - **Dependencies**: T1.4
  - **Estimated Time**: 15 minutes
  - **Status**: ✅ COMPLETE
  - **Command**: `dotnet ef database update`
  - **Acceptance Criteria**:
    - Tables created in database ✅
    - Verify schema with `\d "ServiceHealthInsights"` in psql ✅

#### P1 - High Priority

- [x] **T1.6** - Add Health Insights Configuration
  - **Location**: `Omni.ServiceRegistry.Api/appsettings.json`
  - **Dependencies**: None
  - **Estimated Time**: 30 minutes
  - **Status**: ✅ COMPLETE
  - **Details**:
    ```json
    {
      "HealthInsights": {
        "BatchAnalysisIntervalHours": 24,
        "AnalysisWindowHours": 48,
        "ManualTriggerMaxWindowHours": 168,
        "MinEventsForAnalysis": 3,
        "MaxConcurrentAnalyses": 2,
        "CacheResultsHours": 24,
        "ClientCooldownSeconds": 60,
        "ServerRateLimitPerMinute": 5,
        "OllamaBaseUrl": "http://localhost:11434",
        "OllamaModel": "phi4",
        "MaxTokens": 4500,
        "Temperature": 0.7
      }
    }
    ```
  - **Acceptance Criteria**:
    - Configuration section added ✅
    - All values have sensible defaults ✅
    - appsettings.Development.json overrides if needed (not needed for now) ✅

- [x] **T1.7** - Create Repository Interfaces
  - **Location**: `Omni.ServiceRegistry.Interfaces/`
  - **Dependencies**: T1.1, T1.2
  - **Estimated Time**: 1 hour
  - **Status**: ✅ COMPLETE
  - **Files**:
    - `IHealthInsightsRepository.cs` ✅
    - `IAnalysisTriggerLogRepository.cs` ✅
  - **Methods**:
    - `Task<ServiceHealthInsight?> GetLatestInsightAsync(Guid serviceId)` ✅
    - `Task<List<ServiceHealthInsight>> GetInsightsByServiceAsync(Guid serviceId, DateTime? since)` ✅
    - `Task<ServiceHealthInsight> AddInsightAsync(ServiceHealthInsight insight)` ✅
    - `Task UpdateInsightAsync(ServiceHealthInsight insight)` ✅
    - `Task<AnalysisTriggerLog?> GetLastTriggerAsync(string triggeredBy)` ✅
    - `Task RecordTriggerAsync(AnalysisTriggerLog log)` ✅
  - **Acceptance Criteria**:
    - Interfaces compile ✅
    - Follow repository pattern ✅
    - Async methods with proper return types ✅

- [x] **T1.8** - Implement Repository Classes
  - **Location**: `Omni.ServiceRegistry.Data/Postgres/Repositories/`
  - **Dependencies**: T1.7
  - **Estimated Time**: 2 hours
  - **Status**: ✅ COMPLETE
  - **Files**:
    - `HealthInsightsRepository.cs` ✅
    - `AnalysisTriggerLogRepository.cs` ✅
  - **Acceptance Criteria**:
    - All interface methods implemented ✅
    - Proper error handling ✅
    - Unit tests pass (deferred to testing phase)

#### P2 - Medium Priority

- [x] **T1.9** - Register Services in DI Container
  - **Location**: `Omni.ServiceRegistry.Api/Program.cs`
  - **Dependencies**: T1.8
  - **Estimated Time**: 15 minutes
  - **Status**: ✅ COMPLETE
  - **Details**:
    - Register repositories as scoped services ✅
    - Register configuration options (using existing IConfiguration injection) ✅
  - **Acceptance Criteria**:
    - Services resolve correctly ✅
    - No DI exceptions on startup ✅

---

### PHASE 2: LLM Integration & Analysis Engine

#### P0 - Critical

- [ ] **T2.1** - Create Ollama Client Service
  - **Location**: `Omni.ServiceRegistry.Services/LLM/OllamaService.cs`
  - **Dependencies**: None
  - **Estimated Time**: 3 hours
  - **Details**:
    - HttpClient wrapper for Ollama API
    - Methods: `Task<string> GenerateAsync(string prompt, CancellationToken ct)`
    - Handle streaming responses
    - Parse JSON responses
    - Error handling & retries
  - **NuGet Package**: May need `System.Net.Http.Json`
  - **Acceptance Criteria**:
    - Successfully calls Ollama API
    - Returns LLM response
    - Handles errors gracefully
    - Logs token usage

- [ ] **T2.2** - Create Context Gatherer Service
  - **Location**: `Omni.ServiceRegistry.Services/HealthInsights/ContextGathererService.cs`
  - **Dependencies**: T1.8
  - **Estimated Time**: 4 hours
  - **Details**:
    - Implement 5-layer context gathering:
      1. Service Identity & Config (~500 tokens)
      2. Recent Health Timeline (~2000 tokens)
      3. Historical Patterns (~500 tokens)
      4. Correlation Context (~1000 tokens)
      5. Recent Changes (~500 tokens)
    - Methods:
      - `Task<AnalysisContext> GatherContextAsync(Guid serviceId, bool isManualTrigger)`
      - `Task<List<ServiceHealthEvent>> GetHealthTimelineAsync(Guid serviceId, DateTime since)`
      - `Task<List<Service>> GetCorrelatedServicesAsync(Guid serviceId, DateTime eventTime, TimeSpan window)`
    - Return structured `AnalysisContext` DTO
  - **Acceptance Criteria**:
    - All 5 layers implemented
    - Respects manual vs automatic trigger windows
    - Token count within limits (4500 total)
    - Proper error handling

- [ ] **T2.3** - Create Prompt Template Service
  - **Location**: `Omni.ServiceRegistry.Services/LLM/PromptTemplateService.cs`
  - **Dependencies**: T2.2
  - **Estimated Time**: 2 hours
  - **Details**:
    - System prompt template
    - User prompt template with context injection
    - Format JSON context into readable prompt
    - Token counting utilities
  - **System Prompt**:
    ```
    You are a service health analyst for a microservices platform. 
    Analyze service health events and provide:
    1. Human-readable incident summary (2-3 sentences)
    2. Potential root causes (ranked by likelihood, max 5)
    3. Related service impact analysis
    4. Historical pattern insights
    5. Recommended actions for administrators (max 5)
    
    Output as JSON with keys: summary, rootCauses, correlatedServices, 
    historicalContext, recommendedActions.
    Be concise, technical, and actionable. Focus on facts from the data provided.
    ```
  - **Acceptance Criteria**:
    - Prompts generate valid output
    - JSON parsing works
    - Token limits respected

#### P1 - High Priority

- [ ] **T2.4** - Create Health Insights Analysis Service
  - **Location**: `Omni.ServiceRegistry.Services/HealthInsights/HealthInsightsAnalysisService.cs`
  - **Dependencies**: T2.1, T2.2, T2.3, T1.8
  - **Estimated Time**: 4 hours
  - **Interface**: `IHealthInsightsAnalysisService`
  - **Methods**:
    - `Task<ServiceHealthInsight> AnalyzeServiceAsync(Guid serviceId, string triggeredBy, bool isManualTrigger)`
    - `Task<List<ServiceHealthInsight>> AnalyzeMultipleServicesAsync(List<Guid> serviceIds, string triggeredBy)`
  - **Logic**:
    1. Check if recent analysis exists (cache)
    2. Gather context via ContextGathererService
    3. Build prompt via PromptTemplateService
    4. Call Ollama via OllamaService
    5. Parse LLM response
    6. Create ServiceHealthInsight entity
    7. Save to repository
    8. Return insight
  - **Acceptance Criteria**:
    - End-to-end analysis works
    - Creates insight records in database
    - Handles LLM failures gracefully
    - Respects cache window

- [ ] **T2.5** - Create Analysis Queue Service
  - **Location**: `Omni.ServiceRegistry.Services/HealthInsights/AnalysisQueueService.cs`
  - **Dependencies**: None
  - **Estimated Time**: 2 hours
  - **Details**:
    - In-memory queue (ConcurrentQueue or Channel)
    - Methods:
      - `Task EnqueueAsync(AnalysisRequest request)`
      - `Task<AnalysisRequest?> DequeueAsync(CancellationToken ct)`
      - `int GetQueueDepth()`
    - Priority queue (manual triggers first)
  - **Alternative**: Use Redis for distributed queue (future enhancement)
  - **Acceptance Criteria**:
    - Thread-safe queue operations
    - Priority handling works
    - Graceful shutdown

#### P2 - Medium Priority

- [ ] **T2.6** - Add Noise Reduction Logic
  - **Location**: Update `HealthInsightsAnalysisService`
  - **Dependencies**: T2.4
  - **Estimated Time**: 1 hour
  - **Details**:
    - Skip analysis if:
      - Single UNHEALTHY→HEALTHY transition (transient)
      - Service has < 10 total heartbeats (too new)
      - Same failure pattern within 1 hour (deduplicate)
    - Log skipped analyses
  - **Acceptance Criteria**:
    - Noise events filtered correctly
    - Logs explain why skipped
    - Reduces unnecessary LLM calls

---

### PHASE 3: API & Background Services

#### P0 - Critical

- [ ] **T3.1** - Create Health Insights API Controller
  - **Location**: `Omni.ServiceRegistry.Api/Controllers/HealthInsightsController.cs`
  - **Dependencies**: T2.4, T2.5
  - **Estimated Time**: 3 hours
  - **Endpoints**:
    - `POST /api/v1/insights/analyze` - Trigger analysis
    - `GET /api/v1/insights/service/{serviceId}` - Get insights for service
    - `GET /api/v1/insights/{insightId}` - Get specific insight
    - `GET /api/v1/insights/recent` - Get recent insights (all services)
  - **DTOs**:
    - `AnalysisRequest` (ServiceIds, IsManualTrigger, TriggeredBy)
    - `AnalysisResponse` (JobIds, Message)
    - `ServiceHealthInsightDto` (all insight fields)
  - **Acceptance Criteria**:
    - All endpoints functional
    - Proper validation
    - Rate limiting applied
    - Swagger documentation

- [ ] **T3.2** - Implement Rate Limiting
  - **Location**: `HealthInsightsController`
  - **Dependencies**: T3.1
  - **Estimated Time**: 1 hour
  - **Details**:
    - Server-side: 5 triggers per 5 minutes per admin
    - Check cooldown window (60 seconds minimum)
    - Check for existing pending jobs
    - Return proper HTTP status codes (429 TooManyRequests)
  - **Acceptance Criteria**:
    - Rate limits enforced
    - Proper error messages
    - Retry-After header set

- [ ] **T3.3** - Create Background Analysis Worker
  - **Location**: `Omni.ServiceRegistry.Services/HealthInsights/HealthInsightsWorker.cs`
  - **Dependencies**: T2.4, T2.5
  - **Estimated Time**: 3 hours
  - **Details**:
    - Implement `IHostedService`
    - Poll analysis queue
    - Process jobs sequentially (respect MaxConcurrentAnalyses)
    - Update job status (Queued → Processing → Completed/Failed)
    - Log processing metrics
  - **Acceptance Criteria**:
    - Worker starts with application
    - Processes queue continuously
    - Handles exceptions gracefully
    - Graceful shutdown on app stop

#### P1 - High Priority

- [ ] **T3.4** - Implement Real-time Analysis Triggers
  - **Location**: `Omni.ServiceRegistry.Services/HeartbeatService.cs`
  - **Dependencies**: T2.5
  - **Estimated Time**: 2 hours
  - **Details**:
    - Hook into health status transitions
    - Queue analysis for:
      - HEALTHY → DEAD
      - HEALTHY → DEGRADED
      - DEAD → RECOVERED
    - Skip if noise filter applies
  - **Acceptance Criteria**:
    - Critical transitions trigger analysis
    - Queue integration works
    - No performance impact on heartbeat processing

- [ ] **T3.5** - Implement Batch Analysis Scheduler
  - **Location**: `Omni.ServiceRegistry.Services/HealthInsights/BatchAnalysisScheduler.cs`
  - **Dependencies**: T2.4
  - **Estimated Time**: 2 hours
  - **Details**:
    - Implement `IHostedService`
    - Run daily at configured time (default midnight)
    - Find services with multiple UNHEALTHY events in window
    - Queue batch analysis for each
    - Use configured AnalysisWindowHours
  - **Acceptance Criteria**:
    - Scheduled task runs correctly
    - Configurable schedule
    - Respects window configuration
    - Logs batch results

#### P2 - Medium Priority

- [ ] **T3.6** - Add SignalR Notifications
  - **Location**: `Omni.ServiceRegistry.Api/Hubs/HealthInsightsHub.cs`
  - **Dependencies**: T3.3
  - **Estimated Time**: 2 hours
  - **Details**:
    - New hub or extend existing
    - Methods:
      - `NotifyAnalysisStarted(serviceId, jobId)`
      - `NotifyAnalysisCompleted(serviceId, insightId)`
      - `NotifyAnalysisFailed(serviceId, error)`
    - Send to all connected clients
  - **Acceptance Criteria**:
    - Real-time notifications work
    - Dashboard receives updates
    - Connection handling robust

---

### PHASE 4: Dashboard UI & UX

#### P0 - Critical

- [ ] **T4.1** - Create CooldownTimerService
  - **Location**: `Omni.ServiceRegistry.Dashboard/Services/CooldownTimerService.cs`
  - **Dependencies**: None
  - **Estimated Time**: 2 hours
  - **Details**:
    - Client-side timer service
    - Track cooldowns per key (global, per-service)
    - 1-second tick event
    - Methods:
      - `RegisterCooldown(string key, int seconds)`
      - `int GetRemainingSeconds(string key)`
      - `event Action OnTick`
  - **Acceptance Criteria**:
    - Timer runs accurately
    - Multiple cooldowns tracked
    - No memory leaks
    - Proper disposal

- [ ] **T4.2** - Add Global "AI Analysis" Button (Main Dashboard)
  - **Location**: `Omni.ServiceRegistry.Dashboard/Pages/Index.razor`
  - **Dependencies**: T4.1, T3.1
  - **Estimated Time**: 3 hours
  - **Details**:
    - Button in page header next to "Refresh"
    - States: Ready, Cooldown (countdown), Processing
    - Disabled during cooldown (60s)
    - Call API to trigger global analysis
    - Show toast notification on success/failure
    - SignalR updates for progress
  - **Acceptance Criteria**:
    - Button renders correctly
    - Cooldown countdown displays
    - API integration works
    - Toast notifications appear
    - Button disables during processing

- [ ] **T4.3** - Add Per-Service AI Button (Service Cards)
  - **Location**: `Omni.ServiceRegistry.Dashboard/Pages/Index.razor`
  - **Dependencies**: T4.1, T3.1
  - **Estimated Time**: 2 hours
  - **Details**:
    - Icon button (🤖) in each service card
    - Same cooldown logic (per-service key)
    - Trigger analysis for single service
    - Show loading spinner during processing
  - **Acceptance Criteria**:
    - Button in every service card
    - Per-service cooldown works
    - Single service analysis triggers
    - Visual feedback during processing

- [ ] **T4.4** - Create Health Insights Tab Page
  - **Location**: `Omni.ServiceRegistry.Dashboard/Pages/HealthInsights.razor`
  - **Dependencies**: T3.1
  - **Estimated Time**: 4 hours
  - **Details**:
    - New page route: `/insights`
    - Service selector dropdown (or show all)
    - List of insights with expandable details
    - Display:
      - Summary
      - Root causes (expandable)
      - Correlated services
      - Historical context
      - Recommended actions
      - Timestamp, triggered by, processing time
    - Filters: Service, Date range, Trigger type
    - Pagination
  - **Acceptance Criteria**:
    - Page renders insights list
    - Expandable sections work
    - Filters functional
    - Pagination works
    - Responsive design

#### P1 - High Priority

- [ ] **T4.5** - Add Insights Badge to Service Cards
  - **Location**: `Omni.ServiceRegistry.Dashboard/Pages/Index.razor`
  - **Dependencies**: T3.1, T4.4
  - **Estimated Time**: 1 hour
  - **Details**:
    - Show badge "⚠️ New AI insights" when insight available
    - Badge links to Health Insights tab filtered to that service
    - Badge disappears after viewing (mark as seen)
  - **Acceptance Criteria**:
    - Badge appears when new insight exists
    - Clicking badge navigates correctly
    - Badge state persists properly

- [ ] **T4.6** - Create API Client Methods (Dashboard)
  - **Location**: `Omni.ServiceRegistry.Dashboard/Services/ServiceRegistryApiClient.cs`
  - **Dependencies**: T3.1
  - **Estimated Time**: 1 hour
  - **Methods**:
    - `Task<AnalysisResponse> TriggerAnalysisAsync(AnalysisRequest request)`
    - `Task<List<ServiceHealthInsightDto>> GetServiceInsightsAsync(Guid serviceId)`
    - `Task<ServiceHealthInsightDto?> GetInsightAsync(Guid insightId)`
    - `Task<List<ServiceHealthInsightDto>> GetRecentInsightsAsync(int count)`
  - **Acceptance Criteria**:
    - All methods implemented
    - Error handling
    - Deserializes responses correctly

- [ ] **T4.7** - Integrate SignalR Updates
  - **Location**: `Omni.ServiceRegistry.Dashboard/Services/SignalRHubClient.cs`
  - **Dependencies**: T3.6
  - **Estimated Time**: 2 hours
  - **Details**:
    - Subscribe to analysis events
    - Update UI when analysis completes
    - Show toast notifications
    - Refresh insights list automatically
  - **Acceptance Criteria**:
    - Real-time updates work
    - UI refreshes automatically
    - Toast notifications appear

#### P2 - Medium Priority

- [ ] **T4.8** - Add CSS Styling for AI Components
  - **Location**: `Omni.ServiceRegistry.Dashboard/wwwroot/modern-dashboard.css`
  - **Dependencies**: T4.2, T4.3, T4.4
  - **Estimated Time**: 2 hours
  - **Details**:
    - `.btn-ai-analysis` - Global button style
    - `.btn-icon-ai` - Per-service button style
    - `.insight-card` - Insight display card
    - `.insight-section` - Expandable sections
    - `.cooldown-label` - Countdown text
    - Responsive styles
  - **Acceptance Criteria**:
    - Consistent styling
    - Matches existing dashboard theme
    - Responsive on mobile
    - Loading states visible

- [ ] **T4.9** - Add Insights Tab to Navigation
  - **Location**: `Omni.ServiceRegistry.Dashboard/Shared/NavMenu.razor`
  - **Dependencies**: T4.4
  - **Estimated Time**: 15 minutes
  - **Details**:
    - Add "Health Insights" menu item
    - Icon: 🤖 or brain icon
    - Active state styling
  - **Acceptance Criteria**:
    - Menu item appears
    - Navigation works
    - Active state correct

---

## 🧪 Testing Tasks (Optional but Recommended)

### Unit Tests

- [ ] **T5.1** - Test Ollama Service
  - Mock HTTP responses
  - Test error handling
  - Test token counting

- [ ] **T5.2** - Test Context Gatherer
  - Verify all 5 layers collected
  - Test manual vs automatic windows
  - Test token limits

- [ ] **T5.3** - Test Analysis Service
  - Mock LLM responses
  - Test caching logic
  - Test error scenarios

- [ ] **T5.4** - Test Rate Limiting
  - Verify cooldown enforcement
  - Test server-side limits
  - Test concurrent requests

### Integration Tests

- [ ] **T5.5** - Test Full Analysis Flow
  - Trigger analysis via API
  - Verify insight saved to database
  - Check SignalR notification sent

- [ ] **T5.6** - Test Background Workers
  - Real-time trigger on health transition
  - Batch analysis scheduler
  - Queue processing

### UI Tests (bUnit or Manual)

- [ ] **T5.7** - Test Button States
  - Cooldown countdown
  - Processing state
  - Disabled state

- [ ] **T5.8** - Test Health Insights Page
  - Insights display correctly
  - Filters work
  - Pagination works

---

## 📦 Dependencies & Prerequisites

### External Dependencies
- **Ollama**: Must be installed and running (`http://localhost:11434`)
- **phi4 model**: Must be downloaded (`ollama pull phi4`)
- **PostgreSQL**: Database must be running

### NuGet Packages
- May need: `System.Net.Http.Json` (if not already included)
- Existing packages should cover everything else

### Configuration
- Update `appsettings.json` with HealthInsights section
- Ensure Ollama base URL is correct
- Configure model name if using different model

---

## 🎯 Success Criteria

### Phase 1 Complete
✅ Database schema created  
✅ Migrations applied successfully  
✅ Configuration added  
✅ Repositories implemented and registered  

### Phase 2 Complete
✅ Ollama integration working  
✅ Context gathering returns all 5 layers  
✅ Prompts generate valid LLM responses  
✅ Analysis service creates insight records  

### Phase 3 Complete
✅ API endpoints functional  
✅ Rate limiting enforced  
✅ Background workers processing queue  
✅ Real-time and batch triggers working  

### Phase 4 Complete
✅ UI buttons render and function  
✅ Cooldown timer works correctly  
✅ Health Insights tab displays insights  
✅ SignalR updates refresh UI automatically  

### Full Feature Complete
✅ Admin can trigger analysis on-demand  
✅ Automatic analysis runs daily  
✅ Critical transitions trigger real-time analysis  
✅ Insights display human-readable summaries  
✅ Root causes, correlations, and recommendations shown  
✅ No performance degradation on main workflows  

---

## 📊 Progress Tracking

### Phase 1: Foundation & Data Layer
- **Status**: ✅ COMPLETE
- **Tasks Complete**: 9 / 9
- **Estimated Time**: 8 hours
- **Actual Time**: ~2 hours
- **Blocker**: None

### Phase 2: LLM Integration & Analysis Engine
- **Status**: � Ready to Start
- **Tasks Complete**: 0 / 6
- **Estimated Time**: 16 hours
- **Blocker**: None (Phase 1 complete)

### Phase 3: API & Background Services
- **Status**: 🔴 Not Started
- **Tasks Complete**: 0 / 6
- **Estimated Time**: 13 hours
- **Blocker**: Depends on Phase 2

### Phase 4: Dashboard UI & UX
- **Status**: 🔴 Not Started
- **Tasks Complete**: 0 / 9
- **Estimated Time**: 17 hours
- **Blocker**: Depends on Phase 3

### Overall Progress
- **Total Tasks**: 30 core tasks (+ 8 optional testing)
- **Completed**: 9 (Phase 1 complete!)
- **In Progress**: 0
- **Not Started**: 21
- **Total Estimated Time**: 54 hours (~2 weeks with focused work)
- **Time Spent**: ~2 hours (Phase 1)

---

## 🚀 Getting Started

### Immediate Next Steps
1. Review this implementation plan
2. Confirm priorities and task breakdown
3. Start with **T1.1** - Create ServiceHealthInsight Model
4. Work through Phase 1 tasks sequentially
5. Update progress in this document as tasks complete

### Development Workflow
1. Create feature branch: `feature/health-insights-{phase-number}`
2. Complete tasks within phase
3. Test thoroughly
4. Merge to main when phase complete
5. Move to next phase

### Daily Standup Questions
- Which task are you working on?
- Any blockers?
- What will you complete today?
- Update progress percentages

---

## 📝 Notes & Decisions Log

### 2025-12-31 - Planning Session
- ✅ Agreed on Ollama + phi4 for LLM
- ✅ Agreed on 5-layer context approach
- ✅ Agreed on dual analysis windows (manual vs automatic)
- ✅ Agreed on 60-second cooldown with countdown UI
- ✅ Agreed on Option 3: Both global and per-service buttons
- ✅ Agreed on new Health Insights tab for display

### 2025-12-31 - Phase 1 Implementation Complete
- ✅ Created ServiceHealthInsight model (16 properties, Service navigation)
- ✅ Created AnalysisTriggerLog model (5 properties for cooldown tracking)
- ✅ Added DbSets and entity configurations to ServiceRegistryDbContext
- ✅ Configured proper indexes: ServiceId, GeneratedAt, AnalysisStatus, composite indexes
- ✅ Configured JSONB columns for RootCauses, CorrelatedServices, RecommendedActions, ContextData
- ✅ Generated and applied EF Core migration "AddHealthInsightsTables"
- ✅ Verified database schema in PostgreSQL (both tables created with proper constraints)
- ✅ Added HealthInsights configuration section to appsettings.json (13 settings)
- ✅ Created IHealthInsightsRepository interface (7 methods)
- ✅ Created IAnalysisTriggerLogRepository interface (4 methods)
- ✅ Implemented HealthInsightsRepository (PostgreSQL)
- ✅ Implemented AnalysisTriggerLogRepository (PostgreSQL)
- ✅ Registered repositories in DI container (Program.cs)
- ✅ Build successful, no errors
- **Result**: Database schema ready, configuration in place, repositories wired up

### Future Enhancements (Post-MVP)
- Feedback loop: Mark insights as "Helpful" / "Not Helpful"
- Fine-tune phi4 on platform-specific patterns
- Predictive analysis: "Service likely to degrade in 2 hours"
- Export insights to PDF/CSV
- Integration with external alerting (Slack, PagerDuty)
- Multi-model support (switch between phi4, llama, etc.)

---

## 🆘 Troubleshooting Guide

### Common Issues

**Issue**: Ollama not responding  
**Solution**: Check if Ollama is running: `ollama serve`, verify base URL in config

**Issue**: Context exceeds token limit  
**Solution**: Reduce event count in timeline, adjust layer limits in ContextGatherer

**Issue**: Analysis queue backing up  
**Solution**: Increase MaxConcurrentAnalyses, optimize LLM response time

**Issue**: Cooldown not resetting  
**Solution**: Check CooldownTimerService disposal, verify timer is running

**Issue**: SignalR updates not received  
**Solution**: Check hub connection, verify event subscription, check CORS settings

---

## 📚 Related Documentation

- `Documentation/PROJECT_STATUS_REPORT.md` - Overall project status
- `Documentation/LLM_INTEGRATION_OPPORTUNITIES.md` - Full LLM strategy
- `.github/copilot-instructions.md` - Project architecture guidelines
- `specs/001-register-service/spec.md` - Core service registry specification

---

**Last Updated**: 2025-12-31  
**Document Owner**: Project Team  
**Status**: 📋 Ready for Implementation

