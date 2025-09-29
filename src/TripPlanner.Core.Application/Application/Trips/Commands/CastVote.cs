using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Primitives;
using TripPlanner.Core.Domain.Domain.Aggregates;

namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Placeholder for date option voting handler (not implemented in this iteration).
/// Kept for symmetry with destination voting; implementation may be added later.
/// </summary>
public sealed class CastVoteHandler
{
    // Dependencies
    private readonly ITripRepository _repo;
    private readonly IUnitOfWork _uow;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastVoteHandler"/>.
    /// </summary>
    public CastVoteHandler(ITripRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }
}