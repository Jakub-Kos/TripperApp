using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

internal sealed class TripQueries
{
    private readonly AppDbContext _db;
    public TripQueries(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Trip>> ListAsync(int skip, int take, CancellationToken ct)
    {
        var recs = await _db.Trips
            .AsNoTracking()
            .AsSplitQuery()
            .OrderBy(x => x.Name)
            .Skip(skip).Take(take)
            .Include(t => t.Participants)
            .Include(t => t.DateOptions).ThenInclude(o => o.Votes)
            .Include(t => t.Destinations).ThenInclude(d => d.Images)
            .Include(t => t.Destinations).ThenInclude(d => d.Votes)
            .ToListAsync(ct);

        return recs.Select(TripMap.ToAggregate).ToList();
    }
    
    public async Task<IReadOnlyList<Trip>> ListForUserAsync(Guid userId, bool includeFinished, CancellationToken ct)
    {
        var query = _db.Trips
            .AsNoTracking()
            .Include(t => t.Participants)
            .Where(t => t.Participants.Any(p => p.UserId == userId));

        if (!includeFinished) query = query.Where(t => !t.IsFinished);

        var recs = await query
            .OrderBy(t => t.Name)
            .Include(t => t.DateOptions).ThenInclude(o => o.Votes)
            .Include(t => t.Destinations).ThenInclude(d => d.Images)
            .Include(t => t.Destinations).ThenInclude(d => d.Votes)
            .ToListAsync(ct);

        return recs.Select(TripMap.ToAggregate).ToList();
    }

    public async Task<TripSummaryDto?> GetSummaryAsync(TripId id, CancellationToken ct)
    {
        var trip = await _db.Trips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TripId == id.Value, ct);

        if (trip is null) return null;

        var participants = await _db.Participants
            .AsNoTracking()
            .Where(p => p.TripId == id.Value)
            .Select(p => p.ParticipantId.ToString())
            .ToListAsync(ct);

        var dateOptions = await _db.DateOptions
            .AsNoTracking()
            .Where(o => o.TripId == id.Value)
            .Select(o => new DateOptionDto(
                o.DateOptionId.ToString(),
                o.DateIso,
                _db.DateVotes.Count(v => v.DateOptionId == o.DateOptionId)
            ))
            .ToListAsync(ct);

        return new TripSummaryDto(
            trip.TripId.ToString(),
            trip.Name,
            trip.OrganizerId.ToString(),
            participants,
            dateOptions
        );
    }
}