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
    }
}
