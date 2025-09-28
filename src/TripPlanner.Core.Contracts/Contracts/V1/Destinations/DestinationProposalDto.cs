namespace TripPlanner.Core.Contracts.Contracts.V1.Destinations;

public sealed record DestinationProposalDto(
    Guid DestinationId,
    string Title,
    string? Description,
    string[] ImageUrls,
    int Votes,
    bool IsChosen
);