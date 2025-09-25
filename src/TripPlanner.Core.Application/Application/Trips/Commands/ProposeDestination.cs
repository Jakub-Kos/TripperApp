using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips.Commands;

public sealed class ProposeDestinationHandler
{
    private readonly ITripRepository _repo;
    private readonly IUnitOfWork _uow;

    public ProposeDestinationHandler(ITripRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

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