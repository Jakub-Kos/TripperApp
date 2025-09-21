using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

internal sealed class TripWriter
{
    private readonly AppDbContext _db;
    public TripWriter(AppDbContext db) => _db = db;

    public async Task<bool> AddParticipant(TripId tripId, UserId userId, CancellationToken ct)
    {
        var exists = await _db.Trips.AnyAsync(t => t.TripId == tripId.Value, ct);
        if (!exists) return false;

        var dup = await _db.Participants.AnyAsync(p => p.TripId == tripId.Value && p.UserId == userId.Value, ct);
        if (dup) return true; // idempotent

        _db.Participants.Add(new ParticipantRecord { TripId = tripId.Value, UserId = userId.Value });
        return true; // UoW will save
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
        return id; // UoW will save
    }

    public async Task<bool> CastVote(TripId tripId, DateOptionId dateOptionId, UserId userId, CancellationToken ct)
    {
        var opt = await _db.DateOptions.FirstOrDefaultAsync(o => o.DateOptionId == dateOptionId.Value && o.TripId == tripId.Value, ct);
        if (opt is null) return false;

        var dup = await _db.DateVotes.AnyAsync(v => v.DateOptionId == dateOptionId.Value && v.UserId == userId.Value, ct);
        if (dup) return true; // idempotent

        _db.DateVotes.Add(new DateVoteRecord { DateOptionId = dateOptionId.Value, UserId = userId.Value });
        return true; // UoW will save
    }
}