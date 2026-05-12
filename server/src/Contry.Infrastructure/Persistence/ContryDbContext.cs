using Contry.Domain.Authentication;
using Contry.Domain.TestRecords;
using Contry.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Contry.Infrastructure.Persistence;

public sealed class ContryDbContext(DbContextOptions<ContryDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

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
