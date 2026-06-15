using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Converters;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cleanuparr.Persistence;

/// <summary>
/// Database context for events
/// </summary>
public class EventsContext : DbContext
{
    public DbSet<AppEvent> Events { get; set; }

    public DbSet<ManualEvent> ManualEvents { get; set; }

    public DbSet<Strike> Strikes { get; set; }

    public DbSet<DownloadItem> DownloadItems { get; set; }

    public DbSet<JobRun> JobRuns { get; set; }

    public DbSet<SearchEventData> SearchEventData { get; set; }
    
    public EventsContext()
    {
    }
    
    public EventsContext(DbContextOptions<EventsContext> options) : base(options)
    {
    }
    
    public static EventsContext CreateStaticInstance()
    {
        var optionsBuilder = new DbContextOptionsBuilder<EventsContext>();
        SetDbContextOptions(optionsBuilder);
        return new EventsContext(optionsBuilder.Options);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        SetDbContextOptions(optionsBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<UtcDateTimeOffsetConverter>();
    }

    public static string GetLikePattern(string input)
    {
        input = input.Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
        
        return $"%{input}%";
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppEvent>(entity =>
        {
            entity.HasOne(e => e.Strike)
                .WithMany()
                .HasForeignKey(e => e.StrikeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SearchEventData>(entity =>
        {
            entity.HasOne(s => s.AppEvent)
                .WithOne(e => e.SearchEventData)
                .HasForeignKey<SearchEventData>(s => s.AppEventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Strike>(entity =>
        {
            entity.Property(e => e.Type)
                .HasConversion(new LowercaseEnumConverter<StrikeType>());
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var enumProperties = entityType.ClrType.GetProperties()
                .Where(p => !p.IsDefined(typeof(System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute), true))
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
                    .HasConversion((ValueConverter)converter);
            }
        }
    }
    
    private static void SetDbContextOptions(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            return;
        }
        
        var dbPath = Path.Combine(ConfigurationPathProvider.GetConfigPath(), "events.db");
        optionsBuilder
            .UseSqlite($"Data Source={dbPath}")
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();
    }
} 