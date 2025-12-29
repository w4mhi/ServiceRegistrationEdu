using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Omni.ServiceRegistry.Data.Postgres;

/// <summary>
/// Design-time factory for EF Core migrations
/// </summary>
public class ServiceRegistryDbContextFactory : IDesignTimeDbContextFactory<ServiceRegistryDbContext>
{
    public ServiceRegistryDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ServiceRegistryDbContext> optionsBuilder = 
            new DbContextOptionsBuilder<ServiceRegistryDbContext>();
        
        // Use a default connection string for migrations
        optionsBuilder.UseNpgsql("Host=localhost;Database=serviceregistry;Username=postgres;Password=postgres");
        
        return new ServiceRegistryDbContext(optionsBuilder.Options);
    }
}
