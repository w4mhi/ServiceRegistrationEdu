# Service Registry Constitution

## Core Principles

## Software Engineering Rules

1. **Strong Typing**: Use explicit types, NO 'var' keyword allowed. Improves code readability and catches type errors at compile time. Always declare variable types explicitly (e.g., `string name`, not `var name`).

2. **SOLID Principles**: Follow Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, and Dependency Inversion principles. Each class should have one reason to change, be open for extension but closed for modification, and depend on abstractions not concretions.

3. **High Cohesion, Low Coupling**: Keep related functionality together, minimize dependencies between components. Group closely related methods and data in the same class; avoid creating dependencies between unrelated components.

4. **Unit Testing**: Write unit tests for all new functionality. Use xUnit, Moq, and FluentAssertions. Every new method should have corresponding tests covering normal cases, edge cases, and error conditions.

5. **XML Documentation**: All public APIs must have XML comments documenting purpose, parameters, return values, and exceptions. Use `<summary>`, `<param>`, `<returns>`, and `<exception>` tags for comprehensive documentation.

6. **ILogger Usage**: 
   - Libraries: `ILogger<T>?` (nullable) - logging is optional for reusable libraries
   - Applications: `ILogger<T>` (non-nullable) - applications should always log

7. **ILogger Position**: ALWAYS last parameter in constructor and method signatures. This maintains consistency across the codebase and makes logger parameters immediately recognizable.

8. **IConfiguration Injection**: Inject via constructor ONLY, not method parameters. Configuration should be injected once during object construction, not passed repeatedly to methods.

9. **IConfiguration Direct Usage**: IConfiguration should not be used directly in methods. Read values in constructor and store in fields/properties, or use IOptions pattern for strongly-typed access.

10. **IConfiguration vs IOptions**: Do NOT mix in same class; choose one pattern per class. Either inject IConfiguration and read values manually, OR use IOptions<T> for strongly-typed configuration, but never both.

11. **IConfiguration/IOptions Position**: First parameters in constructor signature (before other dependencies). Configuration parameters should appear before service dependencies for consistent constructor ordering.

12. **No Magic Strings**: Use constants, enums, or configuration values instead. Never hardcode strings like status names, connection strings, or API keys directly in code.

13. **Exception Handling**: Use specific exception types; catch specific exceptions when possible; log errors appropriately. Create custom exception classes for domain-specific errors; avoid catching generic Exception unless at application boundaries.

14. **Single Responsibility Principle**: Each class/method should have one clear purpose. Methods should not exceed 30 lines. If a method grows beyond 30 lines, refactor it into smaller, focused methods with descriptive names.

15. **Meaningful Names**: Use descriptive names that convey intent (e.g., `ProcessHeartbeatAsync`, not `DoStuff`, `HandleData`, or single-letter variables). Names should explain what the code does without requiring comments.

16. **Method Length Limit**: Ensure methods do not exceed 30 lines of code. Long methods should be refactored into smaller, well-named helper methods that each do one thing clearly.

17. **Avoid Deep Nesting**: Refactor complex logic into smaller, named methods. Maximum 3-4 levels of nesting. Use early returns, guard clauses, and extracted methods to reduce nesting depth.

18. **LINQ for Collections**: Use LINQ methods for collection operations (Select, Where, Any, All, FirstOrDefault, etc.) instead of manual loops. LINQ makes intent clearer and reduces boilerplate code.

19. **One Class Per File**: Each class in its own file matching the class name exactly. File `CustomerService.cs` should contain only class `CustomerService`. This makes code navigation and organization intuitive.

20. **Layered Dependencies**:
    - **Models**: Depend only on other Models
    - **Interfaces**: Depend only on Interfaces and Models  
    - **Libraries**: Can depend on any other layer
    
    This dependency flow ensures clean architecture with Models at the core, Interfaces wrapping them, and Libraries implementing the interfaces.

21. **Line Length**: Maximum 120 characters per line. Keeps code readable without horizontal scrolling. Break long lines using proper indentation for method chains or parameter lists.

22. **DRY Principle**: Don't Repeat Yourself - extract common logic into reusable methods/classes. If you write the same code twice, extract it into a method; if you write it three times, extract it into a shared utility class.

23. **Dependency Injection**: Use constructor injection for all services. Register all dependencies in DI container; avoid service locator pattern or `new` keyword for services.

24. **Avoid Static Classes**: Except for extension methods; use dependency injection instead. Static classes make testing difficult and create hidden dependencies. Use interfaces and DI for better testability.

25. **Async/Await**: Use async/await for all I/O operations (database, HTTP, file system). Never block on async code with `.Result` or `.Wait()`. This prevents thread pool starvation and deadlocks.

26. **Naming Conventions**:
    - **PascalCase**: Classes, methods, properties, enums (e.g., `CustomerService`, `GetCustomerById`)
    - **camelCase**: Variables, parameters, private fields (e.g., `customerId`, `firstName`)
    - **NO underscore prefix**: Never use `_fieldName` - this convention is prohibited

27. **No Underscore Prefix**: Variables should never be preceded by '_'. Use plain camelCase for all variables including private fields (e.g., `logger`, not `_logger`).

28. **Always Use 'this' for Fields**: Always use 'this' keyword when accessing class fields and properties defined in the current class. This makes it immediately clear when accessing class members vs. local variables or parameters.

29. **Explicit Usings**: Always use explicit using directives: `<ImplicitUsings>disable</ImplicitUsings>`. This makes dependencies explicit and prevents confusion about where types come from.

30. **Console Logger for Development**: Initialize logger to console for development environment. This enables immediate feedback during local development and debugging without setting up complex logging infrastructure.

31. **No Hard-Coded Values**: Do not use hard-coded values. Define them as constants in appropriate classes or configuration files. Hard-coded values are difficult to maintain and change across multiple locations.

32. **Configuration-Driven Values**: All values for variables which can be changed should be defined in appsettings.json for each environment (appsettings.Development.json, appsettings.Production.json). This enables environment-specific configuration without code changes.

33. **Remove Unused Usings**: Using statements should be removed if not used. Unused imports clutter code and slow down compilation. Use IDE cleanup features or code analysis to remove them automatically.

34. **Clean Build - No Warnings**: Build must complete without any warnings. Treat warnings as errors during development. Enable `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in project files to enforce clean builds.

35. **API Documentation**: Use Scalar (not Swashbuckle) for OpenAPI documentation. Scalar provides modern, interactive API documentation with better UX than Swashbuckle.

36. **Favor Interfaces**: Use interface types in method signatures and constructor parameters for flexibility. Depend on abstractions (interfaces) not concrete implementations to enable easier testing and implementation swapping.

### I. Clean Architecture with Pluggable Components
**Purpose**: Maintain clear separation of concerns and enable component swapping without affecting core logic.

**Guidelines**:
- **Layers**: Models → Interfaces → Services → Data/Infrastructure → API/UI
- **Dependencies**: Inner layers never depend on outer layers
- **Abstractions**: Use interfaces for all external dependencies (storage, messaging, etc.)
- **Testability**: Each layer should be independently testable with mocked dependencies
- **Example**: Multiple storage providers (InMemory, Postgres, Redis) implementing same `IStorageProvider` interface

**Key Learnings**:
- Abstraction layers enable easy testing and provider switching
- Factory pattern helps select implementations at runtime
- Configuration-driven selection simplifies deployment flexibility

### II. API-First Design
**Purpose**: Define contracts before implementation to ensure clear communication and testability.

**Guidelines**:
- **OpenAPI Specification**: Define all endpoints, request/response schemas, and error codes upfront
- **DTOs**: Create dedicated Data Transfer Objects for API contracts (separate from domain models)
- **Versioning**: Use URL versioning (`/api/v1/...`) for breaking changes
- **Status Codes**: Use appropriate HTTP status codes (200, 201, 400, 404, 409, 500, etc.)
- **Documentation**: Auto-generate API docs using Scalar or similar tools

**Key Learnings**:
- DTOs prevent exposing internal models and allow independent evolution
- Clear contracts reduce integration issues between components
- Consistent error responses improve client error handling

### III. Real-Time Updates & User Experience
**Purpose**: Provide responsive, real-time feedback to users without requiring manual refreshes.

**Guidelines**:
- **Dashboard Updates**: Implement auto-refresh mechanisms (polling, SignalR, WebSockets)
- **Refresh Intervals**: Balance between freshness (5-10 seconds) and server load
- **Visual Feedback**: Use colors, badges, icons to convey status at a glance
- **Progressive Enhancement**: Start with working functionality, add real-time features incrementally
- **State Management**: Call `StateHasChanged()` after data updates in Blazor components

**Key Learnings**:
- Timer-based polling is simple and effective for moderate refresh rates
- Colored visual indicators improve comprehension (green=good, red=bad, etc.)
- Consistent color scheme across UI improves user recognition
- Font colors often cleaner than background colors for status display

### IV. Performance Optimization - Measure First
**Purpose**: Avoid premature optimization; optimize based on actual performance data.

**Guidelines**:
- **Start Simple**: Implement straightforward solution first
- **Measure**: Use profiling, logging, metrics to identify actual bottlenecks
- **Optimize**: Only optimize proven hot paths with measurable impact
- **Trade-offs**: Document performance trade-offs (e.g., write frequency vs. data freshness)
- **Rollback Plan**: Keep ability to revert optimizations if they cause issues

**Key Learnings**:
- Premature optimization can introduce bugs and complexity
- Simple solutions are easier to debug and maintain
- Real-time updates are more valuable than reduced write operations
- Always validate optimizations solve actual user problems

### V. Background Processing & Monitoring
**Purpose**: Handle long-running tasks and monitoring without blocking user operations.

**Guidelines**:
- **Background Services**: Use `IHostedService` for continuous monitoring tasks
- **Queue Processing**: Process operations asynchronously (in-memory queues for simple cases)
- **State Machines**: Model complex state transitions explicitly (Healthy → Unhealthy → Degraded → Dead)
- **Idempotency**: Design operations to be safely retried
- **Graceful Shutdown**: Handle cancellation tokens properly

**Key Learnings**:
- Background services enable continuous monitoring without user interaction
- In-memory state caching reduces database queries for frequently accessed data
- State machine patterns make complex transitions explicit and testable
- Heartbeat-based monitoring requires timeout detection and recovery logic

### VI. Testing Strategy
**Purpose**: Ensure code quality through comprehensive testing at appropriate levels.

**Guidelines**:
- **Unit Tests**: Test business logic in isolation with mocked dependencies
- **Integration Tests**: Test API endpoints, database transactions, inter-component communication
- **Component Tests**: Test UI components (bUnit for Blazor)
- **Test Data Builders**: Use builder pattern for creating test entities
- **Arrange-Act-Assert**: Structure tests clearly with AAA pattern
- **Meaningful Names**: Test names should describe what is being tested and expected outcome

**Key Learnings**:
- Separate test projects per layer (Models.Tests, Services.Tests, Api.Tests, etc.)
- Shared test utilities in Common project reduce duplication
- Integration tests catch issues unit tests miss (configuration, serialization, etc.)

### VII. Logging & Observability
**Purpose**: Enable effective debugging, monitoring, and troubleshooting in production.

**Guidelines**:
- **Structured Logging**: Use ILogger with structured parameters (not string concatenation)
- **Log Levels**: 
  - Trace: Detailed diagnostic (method entry/exit, parameter values)
  - Info: General operations (registration submitted, approved, heartbeat received)
  - Warning: Potentially harmful situations (missed heartbeat, degraded status)
  - Error: Errors that don't prevent operation (validation failures, exceptions)
- **Key Events**: Log all state changes, user actions, and error conditions
- **Context**: Include relevant IDs, timestamps, and correlation data

**Key Learnings**:
- Structured logging enables better searching and filtering
- Log all registration, approval, and health status transitions
- Include service IDs and timestamps in all heartbeat-related logs
- Balance verbosity with usefulness (avoid log spam)

### VIII. Configuration Management
**Purpose**: Externalize configuration for flexibility across environments.

**Guidelines**:
- **appsettings.json**: Use hierarchical configuration files
- **Environment Overrides**: Support environment-specific settings (Development, Production)
- **Secrets**: Never commit secrets; use User Secrets (dev) or Key Vault (prod)
- **Options Pattern**: Bind configuration sections to strongly-typed options classes
- **Validation**: Validate configuration at startup

**Key Learnings**:
- Configuration-driven provider selection enables deployment flexibility
- Strongly-typed options prevent configuration errors
- Default values in code provide fallbacks for optional settings

## Development Workflow

### Code Review Requirements
- All code changes require review before merge
- Reviewer checks: SOLID principles, naming conventions, test coverage, documentation
- No direct commits to main branch

### Quality Gates
- All unit tests must pass
- Code coverage minimum: 80% for new code
- No compiler warnings allowed
- API documentation must be up to date

### Deployment Process
- Use multi-stage Docker builds for production images
- Database migrations run as Kubernetes Jobs before deployment
- Health checks required for all services
- Graceful shutdown handling for all background services

## Governance

**Constitution Authority**: This constitution supersedes project-specific practices and style guides.

**Amendment Process**: 
1. Document proposed change with rationale
2. Get team approval
3. Update constitution with version increment
4. Communicate changes to all team members

**Compliance**: 
- All pull requests must verify compliance with constitution rules
- Added complexity must be justified with clear benefits
- Violations should be caught in code review

**Version**: 1.0.0 | **Ratified**: 2025-11-16 | **Last Amended**: 2025-11-16
