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
        var rec = await _db.Trips
            .Include(x => x.Participants)
            .FirstOrDefaultAsync(x => x.TripId == id.Value, ct);

        if (rec is null) return null;

        return MapToDomain(rec);
    }

    public async Task Add(Trip trip, CancellationToken ct)
    {
        var rec = MapToRecord(trip);
        _db.Trips.Add(rec);
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Trip>> List(int skip, int take, CancellationToken ct)
    {
        var recs = await _db.Trips
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Skip(skip).Take(take)
            .Include(x => x.Participants)
            .ToListAsync(ct);

        return recs.Select(MapToDomain).ToList();
    }

    private static Trip MapToDomain(TripRecord r)
    {
        var participants = r.Participants.Select(p => new UserId(p.UserId));
        return Trip.Rehydrate(new TripId(r.TripId), r.Name, new UserId(r.OrganizerId), participants);
    }

    private static TripRecord MapToRecord(Trip t)
    {
        var rec = new TripRecord
        {
            TripId = t.Id.Value,
            Name = t.Name,
            OrganizerId = t.OrganizerId.Value,
            Participants = t.Participants
                .Select(p => new TripParticipantRecord { TripId = t.Id.Value, UserId = p.Value })
                .ToList()
        };
        return rec;
    }
}
