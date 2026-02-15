using Cleanuparr.Persistence.Converters;
using Cleanuparr.Persistence.Models.Auth;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.ApiKey).IsUnique();

            entity.Property(u => u.CreatedAt)
                .HasConversion(new UtcDateTimeConverter());

            entity.Property(u => u.UpdatedAt)
                .HasConversion(new UtcDateTimeConverter());

            entity.Property(u => u.LockoutEnd)
                .HasConversion(new UtcDateTimeConverter());

            entity.HasMany(u => u.RecoveryCodes)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.RefreshTokens)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecoveryCode>(entity =>
        {
            entity.Property(r => r.UsedAt)
                .HasConversion(new UtcDateTimeConverter());
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(r => r.TokenHash).IsUnique();

            entity.Property(r => r.ExpiresAt)
                .HasConversion(new UtcDateTimeConverter());

            entity.Property(r => r.CreatedAt)
                .HasConversion(new UtcDateTimeConverter());

            entity.Property(r => r.RevokedAt)
                .HasConversion(new UtcDateTimeConverter());
        });
    }

    private static void SetDbContextOptions(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            return;
        }

        var dbPath = Path.Combine(ConfigurationPathProvider.GetConfigPath(), "users.db");
        optionsBuilder
            .UseSqlite($"Data Source={dbPath}")
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();
    }
}
