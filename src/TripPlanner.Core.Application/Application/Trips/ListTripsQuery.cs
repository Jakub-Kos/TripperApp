namespace TripPlanner.Core.Application.Application.Trips;

public sealed record ListTripsQuery(int Skip = 0, int Take = 50);