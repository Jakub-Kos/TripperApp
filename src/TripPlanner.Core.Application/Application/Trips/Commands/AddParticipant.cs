using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips;

public sealed record AddParticipantCommand(string TripId, string UserId);

public sealed class AddParticipantHandler
{
    private readonly ITripRepository _repo;
    private readonly IUnitOfWork _uow;
    public AddParticipantHandler(ITripRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<bool> Handle(AddParticipantCommand cmd, CancellationToken ct)
    {
        if (!Guid.TryParse(cmd.TripId, out var t) || !Guid.TryParse(cmd.UserId, out var u))
            return false;

        var ok = await _repo.AddParticipant(new TripId(t), new UserId(u), ct);
        if (!ok) return false;
        await _uow.SaveChanges(ct);
        return true;
    }
}