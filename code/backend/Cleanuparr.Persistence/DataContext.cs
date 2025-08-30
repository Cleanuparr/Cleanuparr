using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Converters;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cleanuparr.Persistence;

/// <summary>
/// Database context for configuration data
/// </summary>
public class DataContext : DbContext
{
    public static SemaphoreSlim Lock { get; } = new(1, 1);
    
    public DbSet<GeneralConfig> GeneralConfigs { get; set; }
    
    public DbSet<DownloadClientConfig> DownloadClients { get; set; }
    
    public DbSet<QueueCleanerConfig> QueueCleanerConfigs { get; set; }
    
    public DbSet<ContentBlockerConfig> ContentBlockerConfigs { get; set; }
    
    public DbSet<DownloadCleanerConfig> DownloadCleanerConfigs { get; set; }
    
    public DbSet<CleanCategory> CleanCategories { get; set; }
    
    public DbSet<ArrConfig> ArrConfigs { get; set; }
    
    public DbSet<ArrInstance> ArrInstances { get; set; }
    
    // New notification system entities
    public DbSet<NotificationProvider> NotificationProviders { get; set; }
    
    public DbSet<NotifiarrConfiguration> NotifiarrConfigurations { get; set; }
    
    public DbSet<AppriseConfiguration> AppriseConfigurations { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            return;
        }
        
        var dbPath = Path.Combine(ConfigurationPathProvider.GetConfigPath(), "cleanuparr.db");
        optionsBuilder
            .UseSqlite($"Data Source={dbPath}")
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QueueCleanerConfig>(entity =>
        {
            entity.ComplexProperty(e => e.FailedImport);
            entity.ComplexProperty(e => e.Stalled);
            entity.ComplexProperty(e => e.Slow);
        });
        
        modelBuilder.Entity<ContentBlockerConfig>(entity =>
        {
            entity.ComplexProperty(e => e.Sonarr, cp =>
            {
                cp.Property(s => s.BlocklistType).HasConversion<LowercaseEnumConverter<BlocklistType>>();
            });
            entity.ComplexProperty(e => e.Radarr, cp =>
            {
                cp.Property(s => s.BlocklistType).HasConversion<LowercaseEnumConverter<BlocklistType>>();
            });
            entity.ComplexProperty(e => e.Lidarr, cp =>
            {
                cp.Property(s => s.BlocklistType).HasConversion<LowercaseEnumConverter<BlocklistType>>();
            });
            entity.ComplexProperty(e => e.Readarr, cp =>
            {
                cp.Property(s => s.BlocklistType).HasConversion<LowercaseEnumConverter<BlocklistType>>();
            });
        });
        
        // Configure ArrConfig -> ArrInstance relationship
        modelBuilder.Entity<ArrConfig>(entity =>
        {
            entity.HasMany(a => a.Instances)
                  .WithOne(i => i.ArrConfig)
                  .HasForeignKey(i => i.ArrConfigId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure new notification system relationships
        modelBuilder.Entity<NotificationProvider>(entity =>
        {
            entity.HasOne(p => p.NotifiarrConfiguration)
                  .WithOne(c => c.Provider)
                  .HasForeignKey<NotifiarrConfiguration>(c => c.ProviderId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(p => p.AppriseConfiguration)
                  .WithOne(c => c.Provider)
                  .HasForeignKey<AppriseConfiguration>(c => c.ProviderId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasIndex(p => p.Name).IsUnique();
        });
        
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var enumProperties = entityType.ClrType.GetProperties()
                .Where(p => p.PropertyType.IsEnum || 
                            (p.PropertyType.IsGenericType && 
                             p.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && 
                             p.PropertyType.GetGenericArguments()[0].IsEnum));

            foreach (var property in enumProperties)
            {
                var enumType = property.PropertyType.IsEnum 
                    ? property.PropertyType 
                    : property.PropertyType.GetGenericArguments()[0];

                var converterType = typeof(LowercaseEnumConverter<>).MakeGenericType(enumType);
                var converter = Activator.CreateInstance(converterType);

                modelBuilder.Entity(entityType.ClrType)
                    .Property(property.Name)
                    .HasConversion((ValueConverter)converter!);
            }
        }
    }
} 