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

    private readonly IDatabaseProvider _provider;

    public EventsContext() : this(DatabaseProviderFactory.Current)
    {
    }

    public EventsContext(IDatabaseProvider provider)
    {
        _provider = provider;
    }

    public EventsContext(DbContextOptions<EventsContext> options) : this(options, DatabaseProviderFactory.Current)
    {
    }

    public EventsContext(DbContextOptions<EventsContext> options, IDatabaseProvider provider) : base(options)
    {
        _provider = provider;
    }

    public static EventsContext CreateStaticInstance()
    {
        IDatabaseProvider provider = DatabaseProviderFactory.Current;
        DbContextOptionsBuilder<EventsContext> optionsBuilder = new();
        provider.ConfigureContext(optionsBuilder, DbContextKind.Events);
        return new EventsContext(optionsBuilder.Options, provider);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        SetDbContextOptions(optionsBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        _provider.ConfigureConventions(configurationBuilder);
    }

    public static string GetLikePattern(string input)
    {
        string escaped = input.ToLowerInvariant()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return $"%{escaped}%";
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        string? schema = _provider.GetSchema(DbContextKind.Events);
        if (schema is not null)
        {
            modelBuilder.HasDefaultSchema(schema);
        }

        modelBuilder.Entity<AppEvent>(entity =>
        {
            entity.Property(e => e.ItemHash)
                .HasConversion(new LowercaseStringConverter());

            entity.HasOne(e => e.Strike)
                .WithMany()
                .HasForeignKey(e => e.StrikeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.JobRun)
                .WithMany(j => j.Events)
                .HasForeignKey(e => e.JobRunId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ManualEvent>(entity =>
        {
            // Race-proof gate: at most one UNRESOLVED event per (Type, ItemHash).
            // Partial unique index — resolved rows are exempt, so history/cooldown is unaffected.
            entity.HasIndex(e => new { e.Type, e.ItemHash })
                .IsUnique()
                .HasFilter(_provider.GetUnresolvedEventFilter());
        });

        modelBuilder.Entity<DownloadItem>(entity =>
        {
            entity.Property(d => d.DownloadId)
                .HasConversion(new LowercaseStringConverter());
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

        modelBuilder.ApplyLowercaseEnumConversions();
    }
    
    private void SetDbContextOptions(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            return;
        }

        _provider.ConfigureContext(optionsBuilder, DbContextKind.Events);
    }
}