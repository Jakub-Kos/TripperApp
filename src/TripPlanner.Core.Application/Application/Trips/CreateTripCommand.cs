namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Command to create a new trip with the specified organizer.
/// </summary>
public sealed record CreateTripCommand(string Name, Guid OrganizerId);