using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Db;

public sealed class AppDbContext : DbContext
{
    public DbSet<TripRecord> Trips => Set<TripRecord>();
    public DbSet<TripParticipantRecord> TripParticipants => Set<TripParticipantRecord>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Trip
        b.Entity<TripRecord>(e =>
        {
            e.ToTable("Trips");
            e.HasKey(x => x.TripId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.OrganizerId).IsRequired();

            e.HasMany(x => x.Participants)
                .WithOne()
                .HasForeignKey(x => x.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Participant
        b.Entity<TripParticipantRecord>(e =>
        {
            e.ToTable("TripParticipants");
            e.HasKey(x => x.Id);
            e.Property(x => x.TripId).IsRequired();
            e.Property(x => x.UserId).IsRequired();
            e.HasIndex(x => new { x.TripId, x.UserId }).IsUnique(); // no dup participants
        });
    }
}