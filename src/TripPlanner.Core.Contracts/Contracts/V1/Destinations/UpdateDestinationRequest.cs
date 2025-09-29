namespace TripPlanner.Core.Contracts.Contracts.V1.Destinations;

public sealed record UpdateDestinationRequest(
    string Title,
    string? Description,
    string[] ImageUrls
);
