namespace TripPlanner.Core.Application.Application.Trips;

public sealed record CastVoteCommand(string TripId, string DateOptionId, string UserId);