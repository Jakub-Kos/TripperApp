namespace TripPlanner.Core.Contracts.Contracts.V1.Trips;

public sealed record TripDto(string TripId, string Name, string OrganizerId);

public sealed record TripSummaryDto(
    string TripId,
    string Name,
    string OrganizerId,
    IReadOnlyList<string> Participants,
    IReadOnlyList<DateOptionDto> DateOptions);

public sealed record DateOptionDto(string DateOptionId, string Date, int VotesCount);

// Requests
public sealed record ProposeDateRequest(string Date);             // "YYYY-MM-DD"
public sealed record CastVoteRequest(string DateOptionId, string UserId);
public sealed record CreateTripRequest(string Name);

// Responses
public sealed record CreateTripResponse(TripDto Trip);