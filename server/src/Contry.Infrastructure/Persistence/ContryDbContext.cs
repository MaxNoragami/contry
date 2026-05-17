using Contry.Domain.Clues;
using Contry.Domain.Datasets;
using Contry.Domain.Authentication;
using Contry.Domain.Ranked;
using Contry.Domain.TestRecords;
using Contry.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Contry.Infrastructure.Persistence;

public sealed class ContryDbContext(DbContextOptions<ContryDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    public DbSet<RankedChallenge> RankedChallenges => Set<RankedChallenge>();

    public DbSet<RankedSession> RankedSessions => Set<RankedSession>();

    public DbSet<RankedGuess> RankedGuesses => Set<RankedGuess>();

    public DbSet<RankedUserStats> RankedUserStats => Set<RankedUserStats>();

    public DbSet<RankedCountryDiscoveryStat> RankedCountryDiscoveryStats => Set<RankedCountryDiscoveryStat>();

    public DbSet<RankedClueUsageStat> RankedClueUsageStats => Set<RankedClueUsageStat>();

    public DbSet<BuiltInDatasetDocument> BuiltInDatasetDocuments => Set<BuiltInDatasetDocument>();

    public DbSet<CluePack> CluePacks => Set<CluePack>();

    public DbSet<TestRecord> TestRecords => Set<TestRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username).HasMaxLength(64).IsRequired();
            entity.Property(user => user.NormalizedUsername).HasMaxLength(64).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.NormalizedEmail).HasMaxLength(320).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(user => user.CreatedAtUtc).IsRequired();

            entity.HasIndex(user => user.NormalizedUsername).IsUnique();
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<RankedChallenge>(entity =>
        {
            entity.ToTable("ranked_challenges");

            entity.HasKey(challenge => challenge.Id);
            entity.Property(challenge => challenge.TargetCountryId).HasMaxLength(16).IsRequired();
            entity.Property(challenge => challenge.ClueSetJson).HasColumnType("jsonb").IsRequired();
            entity.Property(challenge => challenge.CreatedAtUtc).IsRequired();

            entity.HasIndex(challenge => challenge.ChallengeDateUtc).IsUnique();
        });

        modelBuilder.Entity<RankedSession>(entity =>
        {
            entity.ToTable("ranked_sessions");

            entity.HasKey(session => session.Id);
            entity.Property(session => session.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(session => session.StartedAtUtc).IsRequired();

            entity.HasIndex(session => new { session.UserId, session.RankedChallengeId }).IsUnique();
            entity.HasIndex(session => session.RankedChallengeId);

            entity.HasOne(session => session.User)
                .WithMany(user => user.RankedSessions)
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(session => session.RankedChallenge)
                .WithMany(challenge => challenge.Sessions)
                .HasForeignKey(session => session.RankedChallengeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RankedGuess>(entity =>
        {
            entity.ToTable("ranked_guesses");

            entity.HasKey(guess => guess.Id);
            entity.Property(guess => guess.GuessCountryId).HasMaxLength(16).IsRequired();
            entity.Property(guess => guess.GuessCountryName).HasMaxLength(128).IsRequired();
            entity.Property(guess => guess.ResultsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(guess => guess.CreatedAtUtc).IsRequired();

            entity.HasIndex(guess => new { guess.RankedSessionId, guess.AttemptNumber }).IsUnique();
            entity.HasIndex(guess => new { guess.RankedSessionId, guess.GuessCountryId }).IsUnique();

            entity.HasOne(guess => guess.RankedSession)
                .WithMany(session => session.Guesses)
                .HasForeignKey(guess => guess.RankedSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RankedUserStats>(entity =>
        {
            entity.ToTable("ranked_user_stats");

            entity.HasKey(stats => stats.UserId);

            entity.HasOne(stats => stats.User)
                .WithOne(user => user.RankedUserStats)
                .HasForeignKey<RankedUserStats>(stats => stats.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RankedCountryDiscoveryStat>(entity =>
        {
            entity.ToTable("ranked_country_discovery_stats");

            entity.HasKey(stats => new { stats.UserId, stats.CountryId });
            entity.Property(stats => stats.CountryId).HasMaxLength(16).IsRequired();

            entity.HasOne(stats => stats.User)
                .WithMany()
                .HasForeignKey(stats => stats.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RankedClueUsageStat>(entity =>
        {
            entity.ToTable("ranked_clue_usage_stats");

            entity.HasKey(stats => new { stats.UserId, stats.ClueId });
            entity.Property(stats => stats.ClueId).HasMaxLength(64).IsRequired();

            entity.HasOne(stats => stats.User)
                .WithMany()
                .HasForeignKey(stats => stats.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BuiltInDatasetDocument>(entity =>
        {
            entity.ToTable("built_in_dataset_documents");

            entity.HasKey(document => document.Path);
            entity.Property(document => document.Path).HasMaxLength(256).IsRequired();
            entity.Property(document => document.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(document => document.Checksum).HasMaxLength(96).IsRequired();
            entity.Property(document => document.Content).IsRequired();
            entity.Property(document => document.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<CluePack>(entity =>
        {
            entity.ToTable("clue_packs");

            entity.HasKey(pack => pack.Id);
            entity.Property(pack => pack.DatasetId).HasMaxLength(96).IsRequired();
            entity.Property(pack => pack.Label).HasMaxLength(120).IsRequired();
            entity.Property(pack => pack.Description).HasMaxLength(120).IsRequired();
            entity.Property(pack => pack.Type).HasMaxLength(32).IsRequired();
            entity.Property(pack => pack.Comparator).HasMaxLength(32).IsRequired();
            entity.Property(pack => pack.UnitSymbol).HasMaxLength(32);
            entity.Property(pack => pack.Icon).HasMaxLength(64).IsRequired();
            entity.Property(pack => pack.CategoriesJson).HasColumnType("jsonb");
            entity.Property(pack => pack.RowsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(pack => pack.Visibility).HasMaxLength(16).IsRequired();
            entity.Property(pack => pack.CreatedAtUtc).IsRequired();
            entity.Property(pack => pack.UpdatedAtUtc).IsRequired();

            entity.HasIndex(pack => pack.OwnerId);
            entity.HasIndex(pack => new { pack.OwnerId, pack.DatasetId }).IsUnique();
            entity.HasIndex(pack => pack.UpdatedAtUtc);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(pack => pack.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshSession>(entity =>
        {
            entity.ToTable("refresh_sessions");

            entity.HasKey(session => session.Id);
            entity.Property(session => session.SessionFamilyId).IsRequired();
            entity.Property(session => session.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(session => session.CreatedAtUtc).IsRequired();
            entity.Property(session => session.ExpiresAtUtc).IsRequired();

            entity.HasIndex(session => session.TokenHash).IsUnique();
            entity.HasIndex(session => session.ExpiresAtUtc);
            entity.HasIndex(session => session.SessionFamilyId);
            entity.HasIndex(session => session.UserId);

            entity.HasOne(session => session.User)
                .WithMany(user => user.RefreshSessions)
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestRecord>(entity =>
        {
            entity.ToTable("test_records");

            entity.HasKey(record => record.Id);
            entity.Property(record => record.Name).HasMaxLength(128).IsRequired();
            entity.Property(record => record.Notes).HasMaxLength(2048).IsRequired();
            entity.Property(record => record.CreatedAtUtc).IsRequired();

            entity.HasIndex(record => record.UserId);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(record => record.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
