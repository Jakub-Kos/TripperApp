using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Abstractions;

public interface ITripRepository
{
    Task<Trip?> Get(TripId id, CancellationToken ct);
    Task Add(Trip trip, CancellationToken ct);
    Task<IReadOnlyList<Trip>> List(int skip, int take, CancellationToken ct);
    
    Task<bool> AddParticipant(TripId tripId, UserId userId, CancellationToken ct);
    Task<DateOptionId> ProposeDateOption(TripId tripId, DateOnly date, CancellationToken ct);
    Task<bool> CastVote(TripId tripId, DateOptionId dateOptionId, UserId userId, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task<int> SaveChanges(CancellationToken ct);
}