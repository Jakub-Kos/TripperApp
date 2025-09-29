namespace TripPlanner.Core.Application.Application.Trips.Queries;

/// <summary>
/// Query for listing destination proposals for a trip.
/// </summary>
public sealed record ListDestinationsQuery(string TripId);
