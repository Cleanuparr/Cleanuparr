using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Converters;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Persistence.Providers;
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

    public DbSet<SeekerHistory> SeekerHistory { get; set; }

    public DbSet<SearchQueueItem> SearchQueue { get; set; }

    public DbSet<CustomFormatScoreEntry> CustomFormatScoreEntries { get; set; }

    public DbSet<CustomFormatScoreHistory> CustomFormatScoreHistory { get; set; }

    public DbSet<SeekerCommandTracker> SeekerCommandTrackers { get; set; }

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
        DatabaseProviderFactory.Current.ConfigureConventions(configurationBuilder);
    }

    public static string GetLikePattern(string input)
    {
        return DatabaseProviderFactory.Current.EscapeLikePattern(input.ToLowerInvariant());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        string? schema = DatabaseProviderFactory.Current.GetSchema(DbContextKind.Events);
        if (schema is not null)
        {
            modelBuilder.HasDefaultSchema(schema);
        }

        modelBuilder.Entity<AppEvent>(entity =>
        {
            entity.HasOne(e => e.Strike)
                .WithMany()
                .HasForeignKey(e => e.StrikeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.JobRun)
                .WithMany(j => j.Events)
                .HasForeignKey(e => e.JobRunId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Strike>(entity =>
        {
            entity.Property(e => e.Type)
                .HasConversion(new LowercaseEnumConverter<StrikeType>());
        });

        modelBuilder.Entity<ManualEvent>(entity =>
        {
            // Race-proof gate: at most one UNRESOLVED event per (Type, ItemHash).
            // Partial unique index — resolved rows are exempt, so history/cooldown is unaffected.
            entity.HasIndex(e => new { e.Type, e.ItemHash })
                .IsUnique()
                .HasFilter(DatabaseProviderFactory.Current.GetUnresolvedEventFilter());
        });

        modelBuilder.Entity<SeekerHistory>(entity =>
        {
            entity.HasIndex(s => new { s.ArrInstanceId, s.ExternalItemId, s.ItemType, s.SeasonNumber, s.CycleId }).IsUnique();
        });

        modelBuilder.Entity<SearchQueueItem>(entity =>
        {
            entity.HasIndex(s => s.ArrInstanceId);
        });

        modelBuilder.Entity<CustomFormatScoreEntry>(entity =>
        {
            entity.HasIndex(s => new { s.ArrInstanceId, s.ExternalItemId, s.EpisodeId }).IsUnique();
            entity.HasIndex(s => s.LastUpgradedAt);
        });

        modelBuilder.Entity<CustomFormatScoreHistory>(entity =>
        {
            entity.HasIndex(s => new { s.ArrInstanceId, s.ExternalItemId, s.EpisodeId });
            entity.HasIndex(s => s.RecordedAt);
        });

        modelBuilder.Entity<SeekerCommandTracker>(entity =>
        {
            entity.HasIndex(s => s.ArrInstanceId);
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

        DatabaseProviderFactory.Current.ConfigureContext(optionsBuilder, DbContextKind.Events);
    }
}