using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips.Commands;

/// <summary>
/// Handles casting a user's vote for a destination proposal within a trip.
/// </summary>
public sealed class VoteDestinationHandler
{
    // Dependencies
    private readonly ITripRepository _repo;
    private readonly IUnitOfWork _uow;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoteDestinationHandler"/>.
    /// </summary>
    public VoteDestinationHandler(ITripRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    /// <summary>
    /// Casts the vote. Returns false if IDs are invalid, the trip is not found, or domain rules reject the vote.
    /// </summary>
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