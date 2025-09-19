using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips.Commands;

public sealed record VoteDestinationCommand(
    string TripId,
    string DestinationId,
    Guid UserId
);

public sealed class VoteDestinationHandler
{
    private readonly ITripRepository _repo;
    private readonly IUnitOfWork _uow;

    public VoteDestinationHandler(ITripRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<bool> Handle(VoteDestinationCommand cmd, CancellationToken ct)
    {
        if (!Guid.TryParse(cmd.TripId, out var tripGuid)) return false;
        if (!Guid.TryParse(cmd.DestinationId, out var destGuid)) return false;

        var userGuid = cmd.UserId;

        var trip = await _repo.Get(new TripId(tripGuid), ct);
        if (trip is null) return false;

        var ok = trip.VoteDestination(new DestinationId(destGuid), new UserId(userGuid));
        if (!ok) return false;

        await _repo.UpdateAsync(trip, ct);
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}