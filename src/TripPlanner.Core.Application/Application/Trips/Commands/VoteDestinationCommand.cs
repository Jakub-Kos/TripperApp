namespace TripPlanner.Core.Application.Application.Trips.Commands;

public sealed record VoteDestinationCommand(
    string TripId,
    string DestinationId,
    Guid UserId
);
