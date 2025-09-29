namespace TripPlanner.Core.Contracts.Contracts.V1.Destinations;

/// <summary>
/// Request payload to propose a new destination for a trip.
/// </summary>
/// <param name="Title">Short title of the destination.</param>
/// <param name="Description">Optional longer description.</param>
/// <param name="ImageUrls">Optional list of image URLs.</param>
public sealed record ProposeDestinationRequest(
    string Title,
    string? Description,
    string[] ImageUrls
);