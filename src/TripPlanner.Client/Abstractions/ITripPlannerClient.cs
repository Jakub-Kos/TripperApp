using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Client.Abstractions;

public interface ITripPlannerClient
{
    Task<TripDto> CreateTripAsync(CreateTripRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TripDto>> ListTripsAsync(int skip = 0, int take = 50, CancellationToken ct = default);
    Task<TripSummaryDto?> GetTripByIdAsync(string tripId, CancellationToken ct = default);

    Task<bool> AddParticipantAsync(string tripId, AddParticipantRequest request, CancellationToken ct = default);
    Task<string?> ProposeDateOptionAsync(string tripId, ProposeDateRequest request, CancellationToken ct = default);
    Task<bool> CastVoteAsync(string tripId, CastVoteRequest request, CancellationToken ct = default);
}