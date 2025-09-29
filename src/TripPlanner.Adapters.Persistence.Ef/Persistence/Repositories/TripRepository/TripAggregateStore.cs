using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Destination;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

internal sealed class TripAggregateStore
{
    private readonly AppDbContext _db;
    public TripAggregateStore(AppDbContext db) => _db = db;

    public async Task<Trip?> Get(TripId id, CancellationToken ct)
    {
        var r = await _db.Trips
            .AsSplitQuery()
            .Include(t => t.Participants)
            .Include(t => t.DateOptions).ThenInclude(o => o.Votes)
            .Include(t => t.Destinations).ThenInclude(d => d.Images)
            .Include(t => t.Destinations).ThenInclude(d => d.Votes)
            .FirstOrDefaultAsync(t => t.TripId == id.Value, ct);

        return r is null ? null : TripMap.ToAggregate(r);
    }

    public Task AddAsync(Trip trip, CancellationToken ct)
    {
        var rec = TripMap.ToRecord(trip);
        _db.Trips.Add(rec);
        // Unit of Work will call SaveChangesAsync
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Trip trip, CancellationToken ct)
    {
        // Ensure Trip exists
        var exists = await _db.Trips.AnyAsync(t => t.TripId == trip.Id.Value, ct);
        if (!exists) throw new InvalidOperationException("Trip not found.");

        // OPTIONAL: update simple fields if the aggregate allows it (name, organizer)
        var header = await _db.Trips.FirstAsync(t => t.TripId == trip.Id.Value, ct);
        header.Name = trip.Name;
        header.OrganizerId = trip.OrganizerId.Value;

        // Upsert Destinations for this Trip without touching existing votes
        var existing = await _db.Destinations
            .Include(d => d.Images)
            .Where(d => d.TripId == trip.Id.Value)
            .ToListAsync(ct);

        var existingMap = existing.ToDictionary(d => d.DestinationId, d => d);

        foreach (var p in trip.DestinationProposals)
        {
            if (existingMap.TryGetValue(p.Id.Value, out var dest))
            {
                // Update simple fields
                dest.Title = p.Title;
                dest.Description = p.Description;
                dest.IsChosen = p.IsChosen;

                // Sync images (replace with provided list)
                var currentUrls = dest.Images.Select(i => i.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var newUrls = (p.ImageUrls ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Remove images not in new list
                foreach (var img in dest.Images.Where(i => !newUrls.Contains(i.Url)).ToList())
                    _db.DestinationImages.Remove(img);

                // Add missing
                foreach (var url in newUrls.Where(u => !currentUrls.Contains(u)))
                    _db.DestinationImages.Add(new DestinationImageRecord { DestinationId = dest.DestinationId, Url = url });
            }
            else
            {
                // New destination proposal -> create without any votes (votes, if any, are handled elsewhere)
                var rec = new DestinationRecord
                {
                    DestinationId = p.Id.Value,
                    TripId = trip.Id.Value,
                    Title = p.Title,
                    Description = p.Description,
                    IsChosen = p.IsChosen,
                    Images = p.ImageUrls.Select(u => new DestinationImageRecord { Url = u }).ToList()
                };
                await _db.Destinations.AddAsync(rec, ct);
            }
        }
        // Note: We intentionally do not delete destinations that might be missing from the aggregate, to avoid accidental data loss.
        // Unit of Work will SaveChangesAsync
    }
}