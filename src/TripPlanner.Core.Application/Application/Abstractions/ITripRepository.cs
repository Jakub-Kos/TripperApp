using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Abstractions;

/// <summary>
/// Repository abstraction for the Trip aggregate and related projections/operations.
/// </summary>
public interface ITripRepository
{
    /// <summary>Loads the Trip aggregate by ID or returns null if not found.</summary>
    Task<Trip?> Get(TripId id, CancellationToken ct);

    /// <summary>Persists a new Trip aggregate.</summary>
    Task AddAsync(Trip trip, CancellationToken ct);

    /// <summary>Returns a page of trips for simple listings.</summary>
    Task<IReadOnlyList<Trip>> ListAsync(int skip, int take, CancellationToken ct);

    /// <summary>Finds a Trip aggregate by ID (alias of Get in some implementations).</summary>
    Task<Trip?> FindByIdAsync(TripId id, CancellationToken ct = default);

    /// <summary>Persists changes to an existing Trip aggregate.</summary>
    Task UpdateAsync(Trip trip, CancellationToken ct = default);

    /// <summary>Adds a participant to a trip; returns false if already present or invalid.</summary>
    Task<bool> AddParticipant(TripId tripId, UserId userId, CancellationToken ct);

    /// <summary>Proposes a new date option within a trip and returns its identifier.</summary>
    Task<DateOptionId> ProposeDateOption(TripId tripId, DateOnly date, CancellationToken ct);

    /// <summary>Returns a lightweight summary projection for a trip.</summary>
    Task<TripSummaryDto?> GetSummaryAsync(TripId id, CancellationToken ct);
}