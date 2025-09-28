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

        // Replace Destinations for this Trip (simple, consistent v1 approach)
        var existing = await _db.Destinations
            .Where(d => d.TripId == trip.Id.Value)
            .ToListAsync(ct);

        // Preserve creator metadata by DestinationId
        var meta = existing.ToDictionary(d => d.DestinationId, d => new { d.CreatedByUserId, d.CreatedAt });

        _db.Destinations.RemoveRange(existing);

        // Build a map of UserId -> ParticipantId for this trip (only claimed participants)
        var participantMap = await _db.Participants
            .Where(p => p.TripId == trip.Id.Value && p.UserId != null)
            .ToDictionaryAsync(p => p.UserId!.Value, p => p.ParticipantId, ct);

        var newRecs = trip.DestinationProposals.Select(p =>
        {
            meta.TryGetValue(p.Id.Value, out var m);
            return new DestinationRecord
            {
                DestinationId = p.Id.Value,
                TripId = trip.Id.Value,
                Title = p.Title,
                Description = p.Description,
                CreatedByUserId = m?.CreatedByUserId ?? Guid.Empty,
                CreatedAt = m?.CreatedAt ?? default,
                Images = p.ImageUrls.Select(u => new DestinationImageRecord { Url = u }).ToList(),
                Votes = p.VotesBy
                    .Where(v => participantMap.ContainsKey(v.Value))
                    .Select(v => new DestinationVoteRecord { ParticipantId = participantMap[v.Value], UserId = v.Value })
                    .ToList()
            };
        });

        await _db.Destinations.AddRangeAsync(newRecs, ct);
        // Unit of Work will SaveChangesAsync
    }
}