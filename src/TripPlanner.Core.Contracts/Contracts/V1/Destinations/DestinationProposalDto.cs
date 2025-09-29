namespace TripPlanner.Core.Contracts.Contracts.V1.Destinations;

/// <summary>
/// Summary view of a destination proposed for a trip, including current vote tally.
/// </summary>
/// <param name="DestinationId">Server-generated identifier.</param>
/// <param name="Title">Short title of the destination.</param>
/// <param name="Description">Optional longer description.</param>
/// <param name="ImageUrls">Zero or more illustrative image URLs.</param>
/// <param name="Votes">Current number of votes for this destination.</param>
/// <param name="IsChosen">True if selected as the final destination.</param>
public sealed record DestinationProposalDto(
    Guid DestinationId,
    string Title,
    string? Description,
    string[] ImageUrls,
    int Votes,
    bool IsChosen
);