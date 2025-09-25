using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips;

public sealed class GetTripByIdHandler
{
    private readonly ITripRepository _repo;
    public GetTripByIdHandler(ITripRepository repo) => _repo = repo;

    public async Task<TripSummaryDto?> Handle(GetTripByIdQuery q, CancellationToken ct)
    {
        if (!Guid.TryParse(q.TripId, out var idGuid)) return null;
        var trip = await _repo.Get(new TripId(idGuid), ct);
        return trip is null ? null : TripMapping.ToSummary(trip);
    }
}