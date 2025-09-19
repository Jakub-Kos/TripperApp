using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips.Queries;

public sealed record ListDestinationsQuery(string TripId);

public sealed class ListDestinationsHandler
{
    private readonly ITripRepository _repo;

    public ListDestinationsHandler(ITripRepository repo) => _repo = repo;

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
                p.Votes))
            .ToArray();
    }
}