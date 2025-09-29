namespace TripPlanner.Core.Contracts.Contracts.V1.Destinations;

/// <summary>
/// Request payload to update an existing destination proposal.
/// </summary>
/// <param name="Title">New title.</param>
/// <param name="Description">New description (nullable to clear or keep depending on API semantics).</param>
/// <param name="ImageUrls">Full replacement list of image URLs.</param>
public sealed record UpdateDestinationRequest(
    string Title,
    string? Description,
    string[] ImageUrls
);
