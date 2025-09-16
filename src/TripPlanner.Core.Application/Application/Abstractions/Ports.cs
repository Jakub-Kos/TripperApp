using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Abstractions;

public interface ITripRepository
{
    Task<Trip?> Get(TripId id, CancellationToken ct);
    Task Add(Trip trip, CancellationToken ct);
    Task<IReadOnlyList<Trip>> List(int skip, int take, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task<int> SaveChanges(CancellationToken ct);
}