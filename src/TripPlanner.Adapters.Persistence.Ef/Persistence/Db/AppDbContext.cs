using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Db;

public sealed class AppDbContext : DbContext
{
    public DbSet<TripRecord> Trips => Set<TripRecord>();
    public DbSet<TripParticipantRecord> TripParticipants => Set<TripParticipantRecord>();

    public DbSet<DateOptionRecord> DateOptions => Set<DateOptionRecord>();
    public DbSet<DateVoteRecord> DateVotes => Set<DateVoteRecord>();
    
    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<RefreshTokenRecord> RefreshTokens => Set<RefreshTokenRecord>();
    
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        
        b.Entity<UserRecord>(ConfigureUser);
        b.Entity<RefreshTokenRecord>(ConfigureRefreshToken);
        b.Entity<TripRecord>(ConfigureTrip);
        b.Entity<TripParticipantRecord>(ConfigureParticipant);
        b.Entity<DateOptionRecord>(ConfigureDateOption);
        b.Entity<DateVoteRecord>(ConfigureDateVote);
    }

    private static void ConfigureUser(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UserRecord> e)
    {
        e.HasKey(x => x.UserId);
        e.Property(x => x.Email).IsRequired();
        e.HasIndex(x => x.Email).IsUnique();
        e.Property(x => x.DisplayName).IsRequired();
        e.Property(x => x.PasswordHash).IsRequired();
    }

    private static void ConfigureRefreshToken(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<RefreshTokenRecord> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Token).IsRequired();
        e.HasIndex(x => x.Token).IsUnique();
        e.Property(x => x.ExpiresAt).IsRequired();
        e.HasOne(x => x.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
    
    private static void ConfigureTrip(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TripRecord> e)
    {
        e.ToTable("Trips");
        e.HasKey(x => x.TripId);
        e.Property(x => x.Name).IsRequired().HasMaxLength(200);
        e.Property(x => x.OrganizerId).IsRequired();

        e.HasMany(x => x.Participants)
            .WithOne()
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasMany(x => x.DateOptions)
            .WithOne()
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureParticipant(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TripParticipantRecord> e)
    {
        e.ToTable("TripParticipants");
        e.HasKey(x => x.Id);
        e.Property(x => x.TripId).IsRequired();
        e.Property(x => x.UserId).IsRequired();
        e.HasIndex(x => new { x.TripId, x.UserId }).IsUnique();
    }

    private static void ConfigureDateOption(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DateOptionRecord> e)
    {
        e.ToTable("DateOptions");
        e.HasKey(x => x.DateOptionId);
        e.Property(x => x.DateIso).IsRequired();
        e.Property(x => x.TripId).IsRequired();

        e.HasIndex(x => new { x.TripId, x.DateIso }).IsUnique();

        e.HasMany(x => x.Votes)
            .WithOne()
            .HasForeignKey(x => x.DateOptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureDateVote(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DateVoteRecord> e)
    {
        e.ToTable("DateVotes");
        e.HasKey(x => x.Id);
        e.Property(x => x.DateOptionId).IsRequired();
        e.Property(x => x.UserId).IsRequired();
        e.HasIndex(x => new { x.DateOptionId, x.UserId }).IsUnique(); // one vote per user per option
    }
}