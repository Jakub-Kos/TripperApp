namespace TripPlanner.Core.Contracts.Contracts.V1.Destinations;

/// <summary>
/// Request to cast a vote for a destination proposal by a specific user.
/// </summary>
/// <param name="UserId">Identifier of the voting user.</param>
public sealed record VoteDestinationRequest(Guid UserId);