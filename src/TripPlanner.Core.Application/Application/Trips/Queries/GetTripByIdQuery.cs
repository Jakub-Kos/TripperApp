namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Query for retrieving a single trip summary by ID.
/// </summary>
public sealed record GetTripByIdQuery(string TripId);