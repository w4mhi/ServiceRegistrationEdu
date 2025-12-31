using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Data.Postgres;

/// <summary>
/// EF Core DbContext for PostgreSQL storage (R55)
/// </summary>
public class ServiceRegistryDbContext : DbContext
{
    public ServiceRegistryDbContext(DbContextOptions<ServiceRegistryDbContext> options)
        : base(options)
    {
    }

    public DbSet<RegistrationRequest> RegistrationRequests => Set<RegistrationRequest>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceChangeHistory> ServiceChangeHistory => Set<ServiceChangeHistory>();
    public DbSet<ServiceDeletionCycle> ServiceDeletionCycles => Set<ServiceDeletionCycle>();
    public DbSet<ServiceHealthInsight> ServiceHealthInsights => Set<ServiceHealthInsight>();
    public DbSet<AnalysisTriggerLog> AnalysisTriggerLogs => Set<AnalysisTriggerLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        
        base.OnModelCreating(modelBuilder);

        // RegistrationRequest configuration
        modelBuilder.Entity<RegistrationRequest>(entity =>
        {
            entity.HasKey(e => e.RegistrationId);
            
            entity.HasIndex(e => e.ServiceNameNormalized)
                .IsUnique()
                .HasFilter("\"Status\" = 0"); // Unique only for pending requests
            
            entity.HasIndex(e => e.Status);
            
            entity.Property(e => e.ServiceName)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.ServiceNameNormalized)
                .IsRequired()
                .HasMaxLength(64); // SHA256 hex string
            
            entity.Property(e => e.ContactEmail)
                .IsRequired()
                .HasMaxLength(255);
            
            entity.Property(e => e.Endpoints)
                .IsRequired()
                .HasColumnType("jsonb");
            
            entity.Property(e => e.HeartbeatTimeout)
                .IsRequired();
            
            entity.Property(e => e.MaxMissedHeartbeats)
                .IsRequired();
        });

        // Service configuration
        modelBuilder.Entity<Service>(entity =>
        {
            entity.HasKey(e => e.ServiceId);
            
            entity.HasIndex(e => e.ServiceNameNormalized)
                .IsUnique()
                .HasFilter("\"DeletionStatus\" != 2"); // Unique only for non-deleted services
            
            entity.HasIndex(e => e.HealthStatus);
            entity.HasIndex(e => e.LastHeartbeatTimestamp);
            entity.HasIndex(e => e.ContactEmail);
            
            entity.Property(e => e.ServiceName)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.ServiceNameNormalized)
                .IsRequired()
                .HasMaxLength(64);
            
            entity.Property(e => e.ContactEmail)
                .IsRequired()
                .HasMaxLength(255);
            
            entity.Property(e => e.Endpoints)
                .IsRequired()
                .HasColumnType("jsonb");
            
            entity.Property(e => e.RowVersion)
                .IsRowVersion(); // Optimistic concurrency
            
            // Soft delete query filter
            entity.HasQueryFilter(s => s.DeletionStatus != DeletionStatus.Deleted);
        });

        // ServiceChangeHistory configuration
        modelBuilder.Entity<ServiceChangeHistory>(entity =>
        {
            entity.HasKey(e => e.ChangeId);
            
            entity.HasIndex(e => e.ServiceId);
            entity.HasIndex(e => e.ChangedAt);
            
            entity.Property(e => e.ChangeType)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.ChangedBy)
                .IsRequired()
                .HasMaxLength(255);
        });
        
        // ServiceDeletionCycle configuration
        modelBuilder.Entity<ServiceDeletionCycle>(entity =>
        {
            entity.HasKey(e => e.ServiceDeletionCycleId);
            
            entity.HasIndex(e => e.ServiceId);
            entity.HasIndex(e => new { e.ServiceId, e.CycleNumber })
                .IsUnique();
            entity.HasIndex(e => e.DeletionApprovedAt);
            entity.HasIndex(e => e.RestorationApprovedAt);
            
            entity.Property(e => e.DeletionRequestedBy)
                .IsRequired()
                .HasMaxLength(255);
            
            entity.Property(e => e.DeletionReason)
                .IsRequired()
                .HasMaxLength(1000);
            
            entity.Property(e => e.DeletionApprovedBy)
                .HasMaxLength(255);
            
            entity.Property(e => e.RestorationRequestedBy)
                .HasMaxLength(255);
            
            entity.Property(e => e.RestorationReason)
                .HasMaxLength(1000);
            
            entity.Property(e => e.RestorationApprovedBy)
                .HasMaxLength(255);
            
            entity.Property(e => e.RestorationMethod)
                .HasMaxLength(20);
            
            // Foreign key relationship
            entity.HasOne(e => e.Service)
                .WithMany(s => s.DeletionHistory)
                .HasForeignKey(e => e.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Matching query filter to align with Service entity
            // This prevents the warning about mismatched query filters
            entity.HasQueryFilter(sdc => sdc.Service!.DeletionStatus != DeletionStatus.Deleted);
        });
        
        // ServiceHealthInsight configuration
        modelBuilder.Entity<ServiceHealthInsight>(entity =>
        {
            entity.HasKey(e => e.InsightId);
            
            entity.HasIndex(e => e.ServiceId);
            entity.HasIndex(e => e.GeneratedAt);
            entity.HasIndex(e => e.AnalysisStatus);
            entity.HasIndex(e => new { e.ServiceId, e.GeneratedAt });
            
            entity.Property(e => e.TriggerType)
                .IsRequired()
                .HasMaxLength(20);
            
            entity.Property(e => e.TriggeredBy)
                .HasMaxLength(255);
            
            entity.Property(e => e.Summary)
                .IsRequired()
                .HasMaxLength(2000);
            
            entity.Property(e => e.RootCauses)
                .HasMaxLength(5000)
                .HasColumnType("jsonb");
            
            entity.Property(e => e.CorrelatedServices)
                .HasMaxLength(3000)
                .HasColumnType("jsonb");
            
            entity.Property(e => e.HistoricalContext)
                .HasMaxLength(2000);
            
            entity.Property(e => e.RecommendedActions)
                .HasMaxLength(3000)
                .HasColumnType("jsonb");
            
            entity.Property(e => e.LlmModel)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.AnalysisStatus)
                .IsRequired()
                .HasMaxLength(20);
            
            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(1000);
            
            entity.Property(e => e.ContextData)
                .HasColumnType("jsonb");
            
            // Foreign key relationship
            entity.HasOne(e => e.Service)
                .WithMany(s => s.HealthInsights)
                .HasForeignKey(e => e.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // AnalysisTriggerLog configuration
        modelBuilder.Entity<AnalysisTriggerLog>(entity =>
        {
            entity.HasKey(e => e.LogId);
            
            entity.HasIndex(e => e.TriggeredBy);
            entity.HasIndex(e => e.TriggeredAt);
            entity.HasIndex(e => new { e.TriggeredBy, e.TriggeredAt });
            
            entity.Property(e => e.TriggeredBy)
                .IsRequired()
                .HasMaxLength(255);
            
            entity.Property(e => e.RequestType)
                .IsRequired()
                .HasMaxLength(20);
            
            entity.Property(e => e.ServiceIds)
                .HasColumnType("jsonb");
        });
    }
}
