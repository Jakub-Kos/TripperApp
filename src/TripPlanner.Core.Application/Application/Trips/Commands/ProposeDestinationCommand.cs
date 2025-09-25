namespace TripPlanner.Core.Application.Application.Trips.Commands;

public sealed record ProposeDestinationCommand(
    string TripId,
    string Title,
    string? Description,
    IReadOnlyList<string> ImageUrls
);
