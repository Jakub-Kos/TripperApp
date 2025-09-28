using TripPlanner.Core.Contracts.Contracts.V1.Destinations;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Client.Abstractions;

public interface ITripPlannerClient
{
    Task<TripDto> CreateTripAsync(CreateTripRequest request, CancellationToken ct = default);

    // New API: list my trips with optional finished filter
    Task<IReadOnlyList<TripDto>> ListMyTripsAsync(bool includeFinished = false, int skip = 0, int take = 50, CancellationToken ct = default);

    // Legacy/general list (kept for compatibility if needed)
    Task<IReadOnlyList<TripDto>> ListTripsAsync(int skip = 0, int take = 50, CancellationToken ct = default);

    Task<TripSummaryDto?> GetTripByIdAsync(string tripId, CancellationToken ct = default);

    Task<bool> AddParticipantAsync(string tripId, AddParticipantRequest request, CancellationToken ct = default);
    // Date range and voting
    Task<bool> SetDateRangeAsync(string tripId, string startIso, string endIso, CancellationToken ct = default);
    Task<bool> VoteOnDateAsync(string tripId, string dateIso, CancellationToken ct = default);

    // New API: update finished status
    Task<bool> UpdateTripStatusAsync(string tripId, bool isFinished, CancellationToken ct = default);
    
    Task<IReadOnlyList<DestinationProposalDto>?> GetDestinationsAsync(string tripId, CancellationToken ct = default);

    /// <summary>Returns the created destination id (GUID string) or null if trip not found.</summary>
    Task<string?> ProposeDestinationAsync(string tripId, ProposeDestinationRequest request, CancellationToken ct = default);

    /// <summary>Returns true if accepted (204), false on 404.</summary>
    Task<bool> VoteDestinationAsync(string tripId, string destinationId, VoteDestinationRequest request, CancellationToken ct = default);

    // Description APIs
    Task<string?> GetTripDescriptionAsync(string tripId, CancellationToken ct = default);
    Task<(bool ok, bool forbidden)> SetTripDescriptionAsync(string tripId, string description, CancellationToken ct = default);

    // Invites
    /// <summary>Creates an invite and returns (code, url) or null if trip not found.</summary>
    Task<(string code, string url)?> CreateInviteAsync(string tripId, int? expiresInMinutes = null, int? maxUses = null, CancellationToken ct = default);

    /// <summary>Joins a trip by invite code. Returns true on success (204), false on invalid/expired code (400).</summary>
    Task<bool> JoinByCodeAsync(string code, CancellationToken ct = default);
}