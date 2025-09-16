using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Aggregates;   // Trip, DateOptionId
using TripPlanner.Core.Domain.Domain.Primitives;   // TripId, UserId

namespace TripPlanner.Adapters.Persistence.InMemory.Persistence;

public sealed class InMemoryTripRepository : ITripRepository, IUnitOfWork
{
    private readonly List<Trip> _store = new();

    public Task<Trip?> Get(TripId id, CancellationToken ct)
        => Task.FromResult(_store.FirstOrDefault(t => t.Id == id));

    public Task Add(Trip trip, CancellationToken ct)
    {
        _store.Add(trip);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Trip>> List(int skip, int take, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Trip>>(_store.Skip(skip).Take(take).ToList());

    // NEW: targeted mutations
    public Task<bool> AddParticipant(TripId tripId, UserId userId, CancellationToken ct)
    {
        var t = _store.FirstOrDefault(x => x.Id == tripId);
        if (t is null) return Task.FromResult(false);
        t.AddParticipant(userId);
        return Task.FromResult(true);
    }

    public Task<DateOptionId> ProposeDateOption(TripId tripId, DateOnly date, CancellationToken ct)
    {
        var t = _store.FirstOrDefault(x => x.Id == tripId)
                ?? throw new InvalidOperationException("Trip not found.");

        var opt = t.ProposeDate(date);          // returns DateOption
        return Task.FromResult(opt.Id);         // return DateOptionId
    }

    public Task<bool> CastVote(TripId tripId, DateOptionId dateOptionId, UserId userId, CancellationToken ct)
    {
        var t = _store.FirstOrDefault(x => x.Id == tripId);
        if (t is null) return Task.FromResult(false);
        t.CastVote(dateOptionId, userId);
        return Task.FromResult(true);
    }

    public Task<int> SaveChanges(CancellationToken ct) => Task.FromResult(0);
}