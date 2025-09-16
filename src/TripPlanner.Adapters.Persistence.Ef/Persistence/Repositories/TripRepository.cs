using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models;
using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

public sealed class TripRepository : ITripRepository
{
    private readonly AppDbContext _db;
    public TripRepository(AppDbContext db) => _db = db;

    public async Task<Trip?> Get(TripId id, CancellationToken ct)
    {
        var r = await _db.Trips
            .Include(t => t.Participants)
            .Include(t => t.DateOptions).ThenInclude(o => o.Votes)
            .FirstOrDefaultAsync(t => t.TripId == id.Value, ct);

        if (r is null) return null;

        var participants = r.Participants.Select(p => new UserId(p.UserId));
        var options = r.DateOptions.Select(o =>
            (new DateOptionId(o.DateOptionId),
                DateOnly.Parse(o.DateIso),
                o.Votes.Select(v => new UserId(v.UserId))));

        return Trip.Rehydrate(new TripId(r.TripId), r.Name, new UserId(r.OrganizerId), participants, options);
    }

    public async Task Add(Trip trip, CancellationToken ct)
    {
        var rec = new TripRecord
        {
            TripId = trip.Id.Value,
            Name = trip.Name,
            OrganizerId = trip.OrganizerId.Value,
            Participants = trip.Participants
                .Select(p => new TripParticipantRecord { TripId = trip.Id.Value, UserId = p.Value })
                .ToList(),
            DateOptions = trip.DateOptions.Select(o => new DateOptionRecord
            {
                DateOptionId = o.Id.Value,
                TripId = trip.Id.Value,
                DateIso = o.Date.ToString("yyyy-MM-dd"),
                Votes = o.Votes.Select(v => new DateVoteRecord { DateOptionId = o.Id.Value, UserId = v.Value }).ToList()
            }).ToList()
        };
        _db.Trips.Add(rec);
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Trip>> List(int skip, int take, CancellationToken ct)
    {
        var recs = await _db.Trips
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Skip(skip).Take(take)
            .Include(t => t.Participants)
            .Include(t => t.DateOptions).ThenInclude(o => o.Votes)
            .ToListAsync(ct);

        return recs.Select(r =>
        {
            var participants = r.Participants.Select(p => new UserId(p.UserId));
            var options = r.DateOptions.Select(o =>
                (new DateOptionId(o.DateOptionId), DateOnly.Parse(o.DateIso), o.Votes.Select(v => new UserId(v.UserId))));
            return Trip.Rehydrate(new TripId(r.TripId), r.Name, new UserId(r.OrganizerId), participants, options);
        }).ToList();
    }

    // Targeted mutations

    public async Task<bool> AddParticipant(TripId tripId, UserId userId, CancellationToken ct)
    {
        var exists = await _db.Trips.AnyAsync(t => t.TripId == tripId.Value, ct);
        if (!exists) return false;

        var dup = await _db.TripParticipants.AnyAsync(p => p.TripId == tripId.Value && p.UserId == userId.Value, ct);
        if (dup) return true; // idempotent

        _db.TripParticipants.Add(new TripParticipantRecord { TripId = tripId.Value, UserId = userId.Value });
        return true;
    }

    public async Task<DateOptionId> ProposeDateOption(TripId tripId, DateOnly date, CancellationToken ct)
    {
        var exists = await _db.Trips.AnyAsync(t => t.TripId == tripId.Value, ct);
        if (!exists) throw new InvalidOperationException("Trip not found.");

        var iso = date.ToString("yyyy-MM-dd");
        var dup = await _db.DateOptions.AnyAsync(o => o.TripId == tripId.Value && o.DateIso == iso, ct);
        if (dup)
        {
            var existing = await _db.DateOptions.FirstAsync(o => o.TripId == tripId.Value && o.DateIso == iso, ct);
            return new DateOptionId(existing.DateOptionId);
        }

        var id = DateOptionId.New();
        _db.DateOptions.Add(new DateOptionRecord { DateOptionId = id.Value, TripId = tripId.Value, DateIso = iso });
        return id;
    }

    public async Task<bool> CastVote(TripId tripId, DateOptionId dateOptionId, UserId userId, CancellationToken ct)
    {
        var opt = await _db.DateOptions.FirstOrDefaultAsync(o => o.DateOptionId == dateOptionId.Value && o.TripId == tripId.Value, ct);
        if (opt is null) return false;

        var dup = await _db.DateVotes.AnyAsync(v => v.DateOptionId == dateOptionId.Value && v.UserId == userId.Value, ct);
        if (dup) return true; // idempotent

        _db.DateVotes.Add(new DateVoteRecord { DateOptionId = dateOptionId.Value, UserId = userId.Value });
        return true;
    }
}