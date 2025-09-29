namespace TripPlanner.Core.Application.Application.Trips.Commands;

/// <summary>
/// Command to propose a new destination for a trip.
/// </summary>
public sealed record ProposeDestinationCommand(
    string TripId,
    string Title,
    string? Description,
    IReadOnlyList<string> ImageUrls
);
