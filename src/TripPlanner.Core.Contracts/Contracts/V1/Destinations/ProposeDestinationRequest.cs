namespace TripPlanner.Core.Contracts.Contracts.V1.Destinations;

public sealed record ProposeDestinationRequest(
    string Title,
    string? Description,
    string[] ImageUrls
);