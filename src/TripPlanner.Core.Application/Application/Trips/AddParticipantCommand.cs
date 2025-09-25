namespace TripPlanner.Core.Application.Application.Trips;

public sealed record AddParticipantCommand(string TripId, string UserId);