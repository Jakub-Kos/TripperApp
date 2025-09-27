using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Abstractions;

public interface ITripRepository
{
    Task<Trip?> Get(TripId id, CancellationToken ct);
    Task AddAsync(Trip trip, CancellationToken ct);
    Task<IReadOnlyList<Trip>> ListAsync(int skip, int take, CancellationToken ct);
    Task<Trip?> FindByIdAsync(TripId id, CancellationToken ct = default); 
    Task UpdateAsync(Trip trip, CancellationToken ct = default);
    Task<bool> AddParticipant(TripId tripId, UserId userId, CancellationToken ct);
    Task<DateOptionId> ProposeDateOption(TripId tripId, DateOnly date, CancellationToken ct);
    Task<TripSummaryDto?> GetSummaryAsync(TripId id, CancellationToken ct);
}