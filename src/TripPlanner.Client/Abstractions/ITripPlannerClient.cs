using TripPlanner.Core.Contracts.Contracts.V1.Destinations;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Contracts.Contracts.V1.Gear;
using TripPlanner.Core.Contracts.Contracts.V1.Itinerary;
using TripPlanner.Core.Contracts.Contracts.Common.Participants;

namespace TripPlanner.Client.Abstractions;

/// <summary>
/// High-level client interface for TripPlanner backend.
/// Methods are grouped by feature area to aid discoverability.
/// </summary>
public interface ITripPlannerClient
{
    Task<TripDto> CreateTripAsync(CreateTripRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TripDto>> ListMyTripsAsync(bool includeFinished = false, int skip = 0, int take = 50, CancellationToken ct = default);

    // Legacy/general list (kept for compatibility if needed)
    Task<IReadOnlyList<TripDto>> ListTripsAsync(int skip = 0, int take = 50, CancellationToken ct = default);

    Task<TripSummaryDto?> GetTripByIdAsync(string tripId, CancellationToken ct = default);

    Task<bool> DeleteTripAsync(string tripId, CancellationToken ct = default);

    Task<bool> AddParticipantAsync(string tripId, AddParticipantRequest request, CancellationToken ct = default);
    // Date range and voting
    Task<bool> SetDateRangeAsync(string tripId, string startIso, string endIso, CancellationToken ct = default);
    Task<bool> VoteOnDateAsync(string tripId, string dateIso, CancellationToken ct = default);
    Task<bool> UnvoteOnDateAsync(string tripId, string dateIso, CancellationToken ct = default);
    Task<IReadOnlyList<(string Date, bool IsChosen, IReadOnlyList<string> ParticipantIds)>?> ListDateVotesAsync(string tripId, CancellationToken ct = default);
    Task<bool> VoteOnDateProxyAsync(string tripId, string dateIso, string participantId, CancellationToken ct = default);
    Task<bool> UnvoteOnDateProxyAsync(string tripId, string dateIso, string participantId, CancellationToken ct = default);

    // New API: update finished status
    Task<bool> UpdateTripStatusAsync(string tripId, bool isFinished, CancellationToken ct = default);

    // Rename trip
    Task<bool> RenameTripAsync(string tripId, string name, CancellationToken ct = default);
    
    Task<IReadOnlyList<DestinationProposalDto>?> GetDestinationsAsync(string tripId, CancellationToken ct = default);
    Task<Dictionary<string, object>?> GetDestinationAsync(string tripId, string destinationId, CancellationToken ct = default);

    /// <summary>Returns the created destination id (GUID string) or null if trip not found.</summary>
    Task<string?> ProposeDestinationAsync(string tripId, ProposeDestinationRequest request, CancellationToken ct = default);

    /// <summary>Returns true if accepted (204), false on 404.</summary>
    Task<bool> VoteDestinationAsync(string tripId, string destinationId, VoteDestinationRequest request, CancellationToken ct = default);
    Task<bool> UnvoteDestinationAsync(string tripId, string destinationId, CancellationToken ct = default);
    Task<bool> ProxyVoteDestinationAsync(string tripId, string destinationId, string participantId, CancellationToken ct = default);
    Task<bool> ProxyUnvoteDestinationAsync(string tripId, string destinationId, string participantId, CancellationToken ct = default);
    Task<bool> UpdateDestinationAsync(string tripId, string destinationId, UpdateDestinationRequest request, CancellationToken ct = default);
    Task<bool> DeleteDestinationAsync(string tripId, string destinationId, CancellationToken ct = default);
    Task<IReadOnlyList<string>?> GetDestinationVotesAsync(string tripId, string destinationId, CancellationToken ct = default);

    // Description APIs
    Task<string?> GetTripDescriptionAsync(string tripId, CancellationToken ct = default);
    Task<(bool ok, bool forbidden)> SetTripDescriptionAsync(string tripId, string description, CancellationToken ct = default);

    // Invites
    /// <summary>Creates an invite and returns (code, url) or null if trip not found.</summary>
    Task<(string code, string url)?> CreateInviteAsync(string tripId, int? expiresInMinutes = null, int? maxUses = null, CancellationToken ct = default);

    /// <summary>Resolve invite code to trip without joining. Returns (tripId, name) or null on invalid.</summary>
    Task<(string tripId, string name)?> ResolveInviteAsync(string code, CancellationToken ct = default);

    /// <summary>Joins a trip by invite code. Returns true on success (204), false on invalid/expired code (400).</summary>
    Task<bool> JoinByCodeAsync(string code, CancellationToken ct = default);

    // Gear APIs
    Task<IReadOnlyList<GearItemDto>?> ListGearAsync(string tripId, CancellationToken ct = default);
    Task<GearItemDto?> CreateGearItemAsync(string tripId, CreateGearItemRequest request, CancellationToken ct = default);
    Task<GearItemDto?> UpdateGearItemAsync(string tripId, string gearId, UpdateGearItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteGearItemAsync(string tripId, string gearId, CancellationToken ct = default);
    Task<GearItemDto?> CreateGearAssignmentAsync(string tripId, string gearId, CreateGearAssignmentRequest request, CancellationToken ct = default);
    Task<GearItemDto?> UpdateGearAssignmentAsync(string tripId, string gearId, string assignmentId, CreateGearAssignmentRequest request, CancellationToken ct = default);
    Task<bool> DeleteGearAssignmentAsync(string tripId, string gearId, string assignmentId, CancellationToken ct = default);
    Task<bool> BulkCreateGearAsync(string tripId, BulkCreateGearRequest request, CancellationToken ct = default);

    // Term APIs
    Task<bool> ProposeTermAsync(string tripId, string startIso, string endIso, CancellationToken ct = default);
    Task<IReadOnlyList<(string TermId, string Start, string End, int Votes, bool IsChosen)>?> ListTermsAsync(string tripId, CancellationToken ct = default);
    Task<bool> VoteTermAsync(string tripId, string termId, CancellationToken ct = default);
    Task<bool> UnvoteTermAsync(string tripId, string termId, CancellationToken ct = default);
    Task<bool> ChooseTermAsync(string tripId, string termId, CancellationToken ct = default);
    Task<bool> DeleteTermAsync(string tripId, string termId, CancellationToken ct = default);

    // Itinerary APIs: Days
    Task<IReadOnlyList<DayDto>?> ListDaysAsync(string tripId, CancellationToken ct = default);
    Task<DayDto?> CreateDayAsync(string tripId, CreateDayRequest request, CancellationToken ct = default);
    Task<DayDto?> GetDayAsync(string tripId, string dayId, CancellationToken ct = default);
    Task<bool> UpdateDayAsync(string tripId, string dayId, UpdateDayRequest request, CancellationToken ct = default);
    Task<bool> DeleteDayAsync(string tripId, string dayId, CancellationToken ct = default);
    Task<bool> UpdateDayAnchorsAsync(string tripId, string dayId, UpdateDayAnchorsRequest request, CancellationToken ct = default);

    // Itinerary APIs: Items
    Task<IReadOnlyList<DayItemDto>?> ListDayItemsAsync(string tripId, string dayId, CancellationToken ct = default);
    Task<DayItemDto?> CreateDayItemAsync(string tripId, string dayId, CreateDayItemRequest request, CancellationToken ct = default);
    Task<DayItemDto?> GetDayItemAsync(string tripId, string itemId, CancellationToken ct = default);
    Task<bool> UpdateDayItemAsync(string tripId, string itemId, UpdateDayItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteDayItemAsync(string tripId, string itemId, CancellationToken ct = default);
    Task<bool> ReorderDayItemsAsync(string tripId, string dayId, ReorderDayItemsRequest request, CancellationToken ct = default);

    // Itinerary APIs: Routes
    Task<IReadOnlyList<RouteFileDto>?> ListDayRoutesAsync(string tripId, string dayId, CancellationToken ct = default);
    Task<RouteFileDto?> UploadDayRouteAsync(string tripId, string dayId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task<bool> DeleteDayRouteAsync(string tripId, string dayId, int routeId, CancellationToken ct = default);

    // Participants APIs
    Task<IReadOnlyList<ParticipantInfoDto>?> ListParticipantsAsync(string tripId, CancellationToken ct = default);
    Task<string?> CreatePlaceholderAsync(string tripId, string displayName, CancellationToken ct = default);
    Task<bool> UpdateParticipantDisplayNameAsync(string tripId, string participantId, string displayName, CancellationToken ct = default);
    Task<bool> UpdateMyParticipantDisplayNameAsync(string tripId, string displayName, CancellationToken ct = default);
    Task<bool> DeleteParticipantAsync(string tripId, string participantId, CancellationToken ct = default);
    Task<(string code, string url)?> IssueClaimCodeAsync(string tripId, string participantId, int? expiresInMinutes = null, CancellationToken ct = default);
    Task<bool> ClaimPlaceholderAsync(string code, string? displayName = null, CancellationToken ct = default);
    Task<bool> ClaimPlaceholderInTripAsync(string tripId, string participantId, CancellationToken ct = default);

    // Transportation APIs
    Task<IReadOnlyList<(string TransportationId, string Title, string? Description, bool IsChosen)>?> ListTransportationsAsync(string tripId, CancellationToken ct = default);
    Task<string?> CreateTransportationAsync(string tripId, string title, string? description, CancellationToken ct = default);
    Task<bool> UpdateTransportationAsync(string tripId, string transportationId, string title, string? description, CancellationToken ct = default);
    Task<bool> DeleteTransportationAsync(string tripId, string transportationId, CancellationToken ct = default);
    Task<bool> ChooseTransportationAsync(string tripId, string transportationId, CancellationToken ct = default);
    Task<bool> UploadTransportationRouteAsync(string tripId, string transportationId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task<bool> UploadTransportationDocumentAsync(string tripId, string transportationId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
}