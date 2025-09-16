using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Adapters.Persistence.InMemory.Persistence;

public sealed class InMemoryTripRepository : ITripRepository, IUnitOfWork
{
    private readonly List<Trip> _store = new();

    public Task<Trip?> Get(TripId id, CancellationToken ct)
        => Task.FromResult<Trip?>(_store.FirstOrDefault(t => t.Id == id));

    public Task Add(Trip trip, CancellationToken ct)
    {
        _store.Add(trip);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Trip>> List(int skip, int take, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Trip>>(_store.Skip(skip).Take(take).ToList());

    public Task<int> SaveChanges(CancellationToken ct) => Task.FromResult(0);
}