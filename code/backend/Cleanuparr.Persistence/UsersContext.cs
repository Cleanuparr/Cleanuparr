using Cleanuparr.Persistence.Converters;
using Cleanuparr.Persistence.Models.Auth;
using Cleanuparr.Persistence.Providers;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Persistence;

/// <summary>
/// Database context for user authentication data
/// </summary>
public class UsersContext : DbContext
{
    public static SemaphoreSlim Lock { get; } = new(1, 1);

    public DbSet<User> Users { get; set; }

    public DbSet<RecoveryCode> RecoveryCodes { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    /// <summary>
    /// Per-user feature-view records (first-seen timestamps) backing the "NEW" feature badges.
    /// </summary>
    public DbSet<UserFeatureView> UserFeatureViews { get; set; }

    public UsersContext()
    {
    }

    public UsersContext(DbContextOptions<UsersContext> options) : base(options)
    {
    }

    public static UsersContext CreateStaticInstance()
    {
        var optionsBuilder = new DbContextOptionsBuilder<UsersContext>();
        SetDbContextOptions(optionsBuilder);
        return new UsersContext(optionsBuilder.Options);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        SetDbContextOptions(optionsBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        DatabaseProviderFactory.Current.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        string? schema = DatabaseProviderFactory.Current.GetSchema(DbContextKind.Users);
        if (schema is not null)
        {
            modelBuilder.HasDefaultSchema(schema);
        }

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.ApiKey).IsUnique();

            entity.ComplexProperty(u => u.Oidc);

            entity.HasMany(u => u.RecoveryCodes)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.RefreshTokens)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.FeatureViews)
                .WithOne(v => v.User)
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(r => r.TokenHash).IsUnique();
        });

        modelBuilder.Entity<UserFeatureView>(entity =>
        {
            entity.HasIndex(v => new { v.UserId, v.FeatureId }).IsUnique();

            entity.Property(v => v.FeatureId)
                .HasMaxLength(64);
        });
    }

    private static void SetDbContextOptions(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            return;
        }

        DatabaseProviderFactory.Current.ConfigureContext(optionsBuilder, DbContextKind.Users);
    }
}
