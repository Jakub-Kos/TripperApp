namespace TripPlanner.Core.Contracts.Contracts.V1.Trips;

public sealed record TripDto(string TripId, string Name, string OrganizerId);
public sealed record CreateTripRequest(string Name, string OrganizerId);
public sealed record CreateTripResponse(TripDto Trip);