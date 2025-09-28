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
    public DbSet<TermProposalRecord> TermProposals => Set<TermProposalRecord>();
    public DbSet<DestinationRecord> Destinations => Set<DestinationRecord>();
    public DbSet<DestinationImageRecord> DestinationImages => Set<DestinationImageRecord>();
    public DbSet<DestinationVoteRecord> DestinationVotes => Set<DestinationVoteRecord>();
    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<RefreshTokenRecord> RefreshTokens => Set<RefreshTokenRecord>();
    public DbSet<TripInviteRecord> TripInvites => Set<TripInviteRecord>();           
    public DbSet<PlaceholderClaimRecord> PlaceholderClaims => Set<PlaceholderClaimRecord>();

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
        modelBuilder.Entity<TermProposalRecord>(ConfigureTermProposal);

        modelBuilder.Entity<DestinationRecord>(ConfigureDestination);
        modelBuilder.Entity<DestinationImageRecord>(ConfigureDestinationImage);
        modelBuilder.Entity<DestinationVoteRecord>(ConfigureDestinationVote);
        
        modelBuilder.Entity<TripInviteRecord>(ConfigureTripInvite);            
        modelBuilder.Entity<PlaceholderClaimRecord>(ConfigurePlaceholderClaim); 
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
        e.Property(x => x.DescriptionMarkdown).HasDefaultValue(string.Empty);
        
        e.Property(x => x.IsFinished)
            .IsRequired()
            .HasDefaultValue(false);
        
        e.HasMany(x => x.Participants)
            .WithOne(p => p.Trip)
            .HasForeignKey(p => p.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasMany(x => x.DateOptions)
            .WithOne(o => o.Trip)
            .HasForeignKey(o => o.TripId)
            .OnDelete(DeleteBehavior.Cascade);
        
        e.HasMany(x => x.Destinations)
            .WithOne(d => d.Trip)
            .HasForeignKey(d => d.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasMany(x => x.TermProposals)
            .WithOne(t => t.Trip)
            .HasForeignKey(t => t.TripId)
            .OnDelete(DeleteBehavior.Cascade);
    }
    
    private static void ConfigureParticipant(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ParticipantRecord> e)
    {
        e.ToTable("TripParticipants");
        e.HasKey(x => x.Id);
        
        e.Property(x => x.TripId).IsRequired();
        e.Property(x => x.ParticipantId).IsRequired();
        e.Property(x => x.DisplayName).IsRequired();
        
        e.Property(x => x.UserId).IsRequired(false);
        
        e.Property(x => x.ClaimedAt).IsRequired(false);
        e.Property(x => x.CreatedByUserId).IsRequired();
        
        e.HasOne(x => x.Trip)
            .WithMany(t => t.Participants)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);
        
        e.HasOne(x => x.User)
            .WithMany()                            
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Alternate key to allow FKs to ParticipantId from vote tables
        e.HasAlternateKey(x => x.ParticipantId);
        
        e.HasIndex(x => new { x.TripId, x.ParticipantId }).IsUnique();
        e.HasIndex(x => new { x.TripId, x.UserId }).IsUnique();
    }

    private static void ConfigureDateOption(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DateOptionRecord> e)
    {
        e.ToTable("DateOptions");
        e.HasKey(x => x.DateOptionId);
        
        e.Property(x => x.DateIso).IsRequired();
        e.Property(x => x.TripId).IsRequired();
        
        e.HasMany(x => x.Votes)
            .WithOne() 
            .HasForeignKey(v => v.DateOptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureDateVote(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DateVoteRecord> e)
    {
        e.ToTable("DateVotes");
        e.HasKey(x => x.Id);
        
        e.Property(x => x.DateOptionId).IsRequired();
        e.Property(x => x.ParticipantId).IsRequired();
        e.Property(x => x.UserId).IsRequired(false);
        
        e.HasIndex(x => new { x.DateOptionId, x.ParticipantId }).IsUnique();
        
        // FK to participant by alternate key (ParticipantId)
        e.HasOne<ParticipantRecord>()
            .WithMany()
            .HasForeignKey(x => x.ParticipantId)
            .HasPrincipalKey(p => p.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureDestination(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DestinationRecord> e)
    {
        e.ToTable("Destinations");
        e.HasKey(x => x.DestinationId);
        
        e.Property(x => x.TripId).IsRequired();
        e.Property(x => x.Title).IsRequired().HasMaxLength(256);
        e.Property(x => x.Description).HasDefaultValue(string.Empty);

        e.HasMany(x => x.Images)
            .WithOne()
            .HasForeignKey(i => i.DestinationId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasMany(x => x.Votes)
            .WithOne()
            .HasForeignKey(v => v.DestinationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
    
    private static void ConfigureDestinationImage(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DestinationImageRecord> e)
    {
        e.HasOne(img => img.Destination)
            .WithMany(dest => dest.Images)
            .HasForeignKey(img => img.DestinationId)
            .OnDelete(DeleteBehavior.Cascade);

    }
    
    private static void ConfigureDestinationVote(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DestinationVoteRecord> e)
    {
        e.ToTable("DestinationVotes");
        e.HasKey(x => x.Id);
        
        e.Property(x => x.DestinationId).IsRequired();
        e.Property(x => x.ParticipantId).IsRequired(); 
        e.Property(x => x.UserId).IsRequired(false);
        
        e.HasIndex(x => new { x.DestinationId, x.ParticipantId }).IsUnique();
        
        // Bind the navigation to Destination with the same FK to avoid duplicate shadow FKs (DestinationId1)
        e.HasOne(v => v.Destination)
            .WithMany(d => d.Votes)
            .HasForeignKey(v => v.DestinationId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // FK to participant by alternate key (ParticipantId)
        e.HasOne<ParticipantRecord>()
            .WithMany()
            .HasForeignKey(x => x.ParticipantId)
            .HasPrincipalKey(p => p.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
    
    private static void ConfigureTripInvite(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TripInviteRecord> e)
    {
        e.ToTable("TripInvites");
        e.HasKey(x => x.InviteId);

        e.Property(x => x.TripId).IsRequired();
        e.Property(x => x.CodeHash).IsRequired();
        e.Property(x => x.ExpiresAt).IsRequired();
        e.Property(x => x.MaxUses).IsRequired();
        e.Property(x => x.Uses).IsRequired();
        e.Property(x => x.CreatedAt).IsRequired();
        e.Property(x => x.CreatedByUserId).IsRequired();
        e.Property(x => x.RevokedAt).IsRequired(false);

        e.HasIndex(x => x.TripId);
        e.HasIndex(x => x.CodeHash).IsUnique();
    }
    
    private static void ConfigurePlaceholderClaim(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<PlaceholderClaimRecord> e)
    {
        e.ToTable("PlaceholderClaims");
        e.HasKey(x => x.ClaimId);

        e.Property(x => x.TripId).IsRequired();
        e.Property(x => x.ParticipantId).IsRequired();
        e.Property(x => x.CodeHash).IsRequired();
        e.Property(x => x.ExpiresAt).IsRequired();
        e.Property(x => x.CreatedAt).IsRequired();
        e.Property(x => x.CreatedByUserId).IsRequired();
        e.Property(x => x.RevokedAt).IsRequired(false);

        e.HasIndex(x => x.CodeHash).IsUnique();
        e.HasIndex(x => new { x.TripId, x.ParticipantId });
        e.HasIndex(x => x.ExpiresAt);
    }

    private static void ConfigureTermProposal(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TermProposalRecord> e)
    {
        e.ToTable("TermProposals");
        e.HasKey(x => x.TermProposalId);

        e.Property(x => x.TripId).IsRequired();
        e.Property(x => x.StartIso).IsRequired();
        e.Property(x => x.EndIso).IsRequired();

        e.HasIndex(x => new { x.TripId, x.StartIso, x.EndIso });

        e.HasOne(x => x.Trip)
            .WithMany(t => t.TermProposals)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}