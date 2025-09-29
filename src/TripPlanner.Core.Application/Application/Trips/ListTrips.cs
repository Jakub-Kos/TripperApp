using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Handles listing trips with basic paging.
/// </summary>
public sealed class ListTripsHandler
{
    // Dependencies
    private readonly ITripRepository _trips;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListTripsHandler"/>.
    /// </summary>
    public ListTripsHandler(ITripRepository trips) => _trips = trips;

    /// <summary>
    /// Returns a page of trips as lightweight DTOs.
    /// </summary>
    public async Task<IReadOnlyList<TripDto>> Handle(ListTripsQuery q, CancellationToken ct)
    {
        var list = await _trips.ListAsync(q.Skip, q.Take, ct);
        return list
            .Select(t => new TripDto(t.Id.Value.ToString("D"), t.Name, t.OrganizerId.Value.ToString("D")))
            .ToList();
    }
}