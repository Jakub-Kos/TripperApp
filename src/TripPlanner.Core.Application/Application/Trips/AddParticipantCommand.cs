namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Command to add a user as a participant to a trip.
/// </summary>
public sealed record AddParticipantCommand(string TripId, string UserId);