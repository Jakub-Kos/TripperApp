namespace TripPlanner.Core.Application.Application.Trips;

public sealed record CreateTripCommand(string Name, Guid OrganizerId);