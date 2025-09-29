using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips.Commands;

/// <summary>
/// Handles proposing a new destination for a trip.
/// </summary>
public sealed class ProposeDestinationHandler
{
    // Dependencies
    private readonly ITripRepository _repo;
    private readonly IUnitOfWork _uow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProposeDestinationHandler"/>.
    /// </summary>
    public ProposeDestinationHandler(ITripRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    /// <summary>
    /// Proposes a destination and persists the updated trip aggregate.
    /// </summary>
    public async Task<DestinationId?> Handle(ProposeDestinationCommand cmd, CancellationToken ct)
    {
        if (!Guid.TryParse(cmd.TripId, out var tripGuid)) return null;

        var trip = await _repo.Get(new TripId(tripGuid), ct);
        if (trip is null) return null;

        var id = trip.ProposeDestination(cmd.Title, cmd.Description, cmd.ImageUrls);
        await _repo.UpdateAsync(trip, ct);
        await _uow.SaveChangesAsync(ct);

        return id;
    }
}