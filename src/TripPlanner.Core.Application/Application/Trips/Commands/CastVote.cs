using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Primitives;
using TripPlanner.Core.Domain.Domain.Aggregates;
namespace TripPlanner.Core.Application.Application.Trips;

public sealed record CastVoteCommand(string TripId, string DateOptionId, string UserId);

public sealed class CastVoteHandler
{
    private readonly ITripRepository _repo;
    private readonly IUnitOfWork _uow;
    public CastVoteHandler(ITripRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

}