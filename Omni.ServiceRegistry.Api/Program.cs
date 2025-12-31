using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Omni.ServiceRegistry.Api.Hubs;
using Omni.ServiceRegistry.Api.Middleware;
using Omni.ServiceRegistry.Api.Services;
using Omni.ServiceRegistry.Data.InMemory;
using Omni.ServiceRegistry.Data.Postgres;
using Omni.ServiceRegistry.Data.Postgres.Repositories;
using Omni.ServiceRegistry.Data.Repositories;
using Omni.ServiceRegistry.Interfaces;
using Omni.ServiceRegistry.Services;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Add SignalR
builder.Services.AddSignalR();

// Configure CORS
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:5000", "https://localhost:5001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true); // Required for SignalR
    });
});

// Configure health checks
builder.Services.AddHealthChecks();

// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("heartbeat", config =>
    {
        config.PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:Heartbeat:PermitLimit", 200);
        config.Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("RateLimiting:Heartbeat:WindowSeconds", 1));
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = builder.Configuration.GetValue<int>("RateLimiting:Heartbeat:QueueLimit", 50);
    });
    
    options.AddSlidingWindowLimiter("registration", config =>
    {
        config.PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:Registration:PermitLimit", 10);
        config.Window = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RateLimiting:Registration:WindowMinutes", 1));
        config.SegmentsPerWindow = builder.Configuration.GetValue<int>("RateLimiting:Registration:SegmentsPerWindow", 6);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = builder.Configuration.GetValue<int>("RateLimiting:Registration:QueueLimit", 5);
    });
    
    // Rate limiting for restoration endpoints
    // Max 5 restorations per admin per hour (configurable)
    options.AddFixedWindowLimiter("restoration", config =>
    {
        config.PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:Restoration:PermitLimit", 5);
        config.Window = TimeSpan.FromHours(builder.Configuration.GetValue<int>("RateLimiting:Restoration:WindowHours", 1));
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = builder.Configuration.GetValue<int>("RateLimiting:Restoration:QueueLimit", 2);
    });
    
    // Rate limiting for health insights analysis endpoints
    // Server-side limit: 5 requests per 5 minutes per user
    options.AddFixedWindowLimiter("insights", config =>
    {
        config.PermitLimit = 5;
        config.Window = TimeSpan.FromMinutes(5);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 2;
    });
});

// Register core services
builder.Services.AddSingleton<IServiceNameNormalizer, ServiceNameNormalizer>();
builder.Services.AddSingleton<IRegistrationValidator, RegistrationValidator>();
builder.Services.AddSingleton<IHealthStatusNotifier, SignalRHealthStatusNotifier>();

// Configure storage based on DatabaseProvider setting in appsettings.json
// If not specified, use InMemory for Development, Postgres for Production
string databaseProvider = builder.Configuration["DatabaseProvider"] 
    ?? (builder.Environment.IsDevelopment() ? "InMemory" : "Postgres");

if (databaseProvider == "Postgres")
{
    // PostgreSQL storage
    string connectionString = builder.Configuration.GetConnectionString("ServiceRegistry")
        ?? throw new InvalidOperationException("ServiceRegistry connection string not configured");
    
    builder.Services.AddDbContext<ServiceRegistryDbContext>(options =>
        options.UseNpgsql(connectionString));
    
    builder.Services.AddScoped<IRegistrationRepository, PostgresRegistrationRepository>();
    builder.Services.AddScoped<IServiceRepository, PostgresServiceRepository>();
    builder.Services.AddScoped<IChangeHistoryRepository, PostgresChangeHistoryRepository>();
    builder.Services.AddScoped<IDeletionCycleRepository, DeletionCycleRepository>();
    builder.Services.AddScoped<IHealthInsightsRepository, HealthInsightsRepository>();
    builder.Services.AddScoped<IAnalysisTriggerLogRepository, AnalysisTriggerLogRepository>();
}
else
{
    // In-memory storage (default for Development)
    builder.Services.AddSingleton<IRegistrationRepository, InMemoryRegistrationRepository>();
    builder.Services.AddSingleton<IServiceRepository, InMemoryServiceRepository>();
}

builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IAdministratorService, AdministratorService>();
builder.Services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
builder.Services.AddScoped<IHeartbeatService, HeartbeatService>();
builder.Services.AddScoped<IContractValidationService, ContractValidationService>();

// Register HttpClient for LLM services
builder.Services.AddHttpClient();

// Register LLM and Health Insights services
builder.Services.AddSingleton<Omni.ServiceRegistry.Services.LLM.OllamaService>();
builder.Services.AddScoped<Omni.ServiceRegistry.Services.HealthInsights.ContextGathererService>();

// Register PromptTemplateService with configurable SystemPrompt
builder.Services.AddSingleton<Omni.ServiceRegistry.Services.LLM.PromptTemplateService>(serviceProvider =>
{
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    string? systemPrompt = configuration.GetValue<string>("HealthInsights:SystemPrompt");
    return new Omni.ServiceRegistry.Services.LLM.PromptTemplateService(systemPrompt);
});

builder.Services.AddScoped<IHealthInsightsAnalysisService, Omni.ServiceRegistry.Services.HealthInsights.HealthInsightsAnalysisService>();
builder.Services.AddSingleton<Omni.ServiceRegistry.Services.HealthInsights.AnalysisQueueService>();

// Register background services - HeartbeatMonitorService and RestorationBadgeCleanupService create their own scopes
builder.Services.AddHostedService<HeartbeatMonitorService>();
builder.Services.AddHostedService<RestorationBadgeCleanupService>();
builder.Services.AddHostedService<HealthInsightsWorker>();
builder.Services.AddHostedService<BatchAnalysisScheduler>(); // Configure via HealthInsights:EnableAutomaticAIAnalysis

WebApplication app = builder.Build();

// Configure the HTTP request pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("DashboardPolicy");
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// SignalR hub endpoint
app.MapHub<ServiceMonitorHub>("/hubs/servicemonitor");

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
