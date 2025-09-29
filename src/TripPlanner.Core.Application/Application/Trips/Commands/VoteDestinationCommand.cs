namespace TripPlanner.Core.Application.Application.Trips.Commands;

/// <summary>
/// Command to cast a user's vote for a specific destination proposal.
/// </summary>
public sealed record VoteDestinationCommand(
    string TripId,
    string DestinationId,
    Guid UserId
);
