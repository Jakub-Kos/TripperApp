namespace TripPlanner.Core.Contracts.Contracts.V1.Trips;

/// <summary>
/// Minimal trip representation used in lists and creation responses.
/// </summary>
/// <param name="TripId">Identifier of the trip.</param>
/// <param name="Name">Human-readable name.</param>
/// <param name="OrganizerId">Participant ID of the organizer.</param>
public sealed record TripDto(string TripId, string Name, string OrganizerId);

/// <summary>
/// Detailed trip summary with status and participants.
/// </summary>
/// <param name="TripId">Identifier of the trip.</param>
/// <param name="Name">Trip name.</param>
/// <param name="OrganizerId">Organizer participant ID.</param>
/// <param name="Description">Short description shown in UI.</param>
/// <param name="CreatedAt">ISO timestamp when the trip was created.</param>
/// <param name="IsFinished">True if the trip has concluded/finalized.</param>
/// <param name="Participants">List of participant IDs.</param>
public sealed record TripSummaryDto(
    string TripId,
    string Name,
    string OrganizerId,
    string Description,
    string CreatedAt,
    bool IsFinished,
    IReadOnlyList<string> Participants);

/// <summary>
/// A proposed date option for the trip along with its current vote state.
/// </summary>
/// <param name="DateOptionId">Identifier of the date option.</param>
/// <param name="Date">ISO date (YYYY-MM-DD).</param>
/// <param name="VotesCount">Number of votes cast for this option.</param>
/// <param name="IsChosen">True if chosen as the final date.</param>
public sealed record DateOptionDto(string DateOptionId, string Date, int VotesCount, bool IsChosen);

// Requests
/// <summary>Propose a new date option for the trip.</summary>
public sealed record ProposeDateRequest(string Date);             // "YYYY-MM-DD"

/// <summary>Cast a vote for a date option as a specific user.</summary>
public sealed record CastVoteRequest(string DateOptionId, string UserId);

/// <summary>Create a new trip.</summary>
public sealed record CreateTripRequest(string Name);

// Responses
/// <summary>Response returned after creating a trip.</summary>
public sealed record CreateTripResponse(TripDto Trip);