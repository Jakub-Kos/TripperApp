using TripPlanner.Core.Contracts.Contracts.V1.Destinations;
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
    
    Task<IReadOnlyList<DestinationProposalDto>?> GetDestinationsAsync(string tripId, CancellationToken ct = default);

    /// <summary>Returns the created destination id (GUID string) or null if trip not found.</summary>
    Task<string?> ProposeDestinationAsync(string tripId, ProposeDestinationRequest request, CancellationToken ct = default);

    /// <summary>Returns true if accepted (204), false on 404.</summary>
    Task<bool> VoteDestinationAsync(string tripId, string destinationId, VoteDestinationRequest request, CancellationToken ct = default);
}