namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Command to cast a vote for a proposed date option.
/// </summary>
public sealed record CastVoteCommand(string TripId, string DateOptionId, string UserId);