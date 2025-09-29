using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Primitives;
using TripPlanner.Core.Domain.Domain.Aggregates; // for DateOptionId
namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Handles proposing a new date option for a trip.
/// </summary>
public sealed class ProposeDateOptionHandler
{
    // Dependencies
    private readonly ITripRepository _repo;
    private readonly IUnitOfWork _uow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProposeDateOptionHandler"/>.
    /// </summary>
    public ProposeDateOptionHandler(ITripRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    /// <summary>
    /// Proposes a date option and persists the change.
    /// </summary>
    public async Task<DateOptionId?> Handle(ProposeDateOptionCommand cmd, CancellationToken ct)
    {
        if (!Guid.TryParse(cmd.TripId, out var t)) return null;

        var id = await _repo.ProposeDateOption(new TripId(t), cmd.Date, ct);
        await _uow.SaveChangesAsync(ct);
        return id;
    }
}