using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Common;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Destination;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Db;

public sealed class AppDbContext : DbContext
{
    public DbSet<TripRecord> Trips => Set<TripRecord>();
    public DbSet<ParticipantRecord> Participants => Set<ParticipantRecord>();
    public DbSet<DateOptionRecord> DateOptions => Set<DateOptionRecord>();
    public DbSet<DateVoteRecord> DateVotes => Set<DateVoteRecord>();
    public DbSet<DestinationRecord> Destinations => Set<DestinationRecord>();
    public DbSet<DestinationImageRecord> DestinationImages => Set<DestinationImageRecord>();
    public DbSet<DestinationVoteRecord> DestinationVotes => Set<DestinationVoteRecord>();
    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<RefreshTokenRecord> RefreshTokens => Set<RefreshTokenRecord>();
    
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<UserRecord>(ConfigureUser);
        modelBuilder.Entity<RefreshTokenRecord>(ConfigureRefreshToken);
        modelBuilder.Entity<TripRecord>(ConfigureTrip);
        modelBuilder.Entity<ParticipantRecord>(ConfigureParticipant);
        
        modelBuilder.Entity<DateOptionRecord>(ConfigureDateOption);
        modelBuilder.Entity<DateVoteRecord>(ConfigureDateVote);

        modelBuilder.Entity<DestinationRecord>(ConfigureDestination);
        modelBuilder.Entity<DestinationImageRecord>(ConfigureDestinationImage);
        modelBuilder.Entity<DestinationVoteRecord>(ConfigureDestinationVote);
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
        e.Property(x => x.CreatedAt).IsRequired();
        e.Property(x => x.DescriptionMarkdown).HasDefaultValue("");

        e.HasMany(x => x.Participants)
            .WithOne(p => p.Trip)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasMany(x => x.DateOptions)
            .WithOne()
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureParticipant(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ParticipantRecord> e)
    {
        e.ToTable("TripParticipants");
        e.HasKey(x => x.Id);
        e.Property(x => x.TripId).IsRequired();
        e.Property(x => x.DisplayName).IsRequired();
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

    private static void ConfigureDestination(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DestinationRecord> e)
    {
        e.HasKey(x => x.DestinationId);
        e.Property(x => x.Title).IsRequired().HasMaxLength(256);
        e.HasOne(x => x.Trip).WithMany(t => t.Destinations).HasForeignKey(x => x.TripId);
        e.HasMany(x => x.Images).WithOne(i => i.Destination).HasForeignKey(i => i.DestinationId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Votes).WithOne(v => v.Destination).HasForeignKey(v => v.DestinationId).OnDelete(DeleteBehavior.Cascade);
    }
    
    private static void ConfigureDestinationImage(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DestinationImageRecord> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Url).IsRequired().HasMaxLength(2048);
    }
    
    private static void ConfigureDestinationVote(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DestinationVoteRecord> e)
    {
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.DestinationId, x.UserId }).IsUnique(); // one vote per user
    }
}