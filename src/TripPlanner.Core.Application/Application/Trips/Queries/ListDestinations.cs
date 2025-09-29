using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips.Queries;

/// <summary>
/// Lists destination proposals for a given trip.
/// </summary>
public sealed class ListDestinationsHandler
{
    // Dependencies
    private readonly ITripRepository _repo;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListDestinationsHandler"/>.
    /// </summary>
    public ListDestinationsHandler(ITripRepository repo) => _repo = repo;

    /// <summary>
    /// Returns destination proposals for the specified trip or null if the trip does not exist/ID is invalid.
    /// </summary>
    public async Task<IReadOnlyList<DestinationProposalDto>?> Handle(ListDestinationsQuery q, CancellationToken ct)
    {
        if (!Guid.TryParse(q.TripId, out var tripGuid)) return null;

        var trip = await _repo.Get(new TripId(tripGuid), ct);
        if (trip is null) return null;

        return trip.DestinationProposals
            .Select(p => new DestinationProposalDto(
                p.Id.Value,
                p.Title,
                p.Description,
                p.ImageUrls.ToArray(),
                p.Votes,
                p.IsChosen))
            .ToArray();
    }
}