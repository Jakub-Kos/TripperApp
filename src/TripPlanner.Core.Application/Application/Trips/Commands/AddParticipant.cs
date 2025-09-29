using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Handles adding a participant to a trip.
/// </summary>
public sealed class AddParticipantHandler
{
    // Dependencies
    private readonly ITripRepository _repo;
    private readonly IUnitOfWork _uow;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddParticipantHandler"/>.
    /// </summary>
    public AddParticipantHandler(ITripRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    /// <summary>
    /// Adds the participant. Returns false if IDs are invalid or the domain rejects the operation.
    /// </summary>
    public async Task<bool> Handle(AddParticipantCommand cmd, CancellationToken ct)
    {
        if (!Guid.TryParse(cmd.TripId, out var t) || !Guid.TryParse(cmd.UserId, out var u))
            return false;

        var ok = await _repo.AddParticipant(new TripId(t), new UserId(u), ct);
        if (!ok) return false;
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}