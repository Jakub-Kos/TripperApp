using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Core.Application.Application.Trips;

public sealed class ListTripsHandler
{
    private readonly ITripRepository _trips;
    public ListTripsHandler(ITripRepository trips) => _trips = trips;

    public async Task<IReadOnlyList<TripDto>> Handle(ListTripsQuery q, CancellationToken ct)
    {
        var list = await _trips.ListAsync(q.Skip, q.Take, ct);
        return list.Select(t => new TripDto(t.Id.Value.ToString("D"), t.Name, t.OrganizerId.Value.ToString("D"))).ToList();
    }
}