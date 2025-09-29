namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Query for listing trips with simple paging.
/// </summary>
public sealed record ListTripsQuery(int Skip = 0, int Take = 50);