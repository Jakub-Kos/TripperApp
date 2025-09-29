using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Returns a detailed summary for a single trip by ID.
/// </summary>
public sealed class GetTripByIdHandler
{
    // Dependencies
    private readonly ITripRepository _repo;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTripByIdHandler"/>.
    /// </summary>
    public GetTripByIdHandler(ITripRepository repo) => _repo = repo;

    /// <summary>
    /// Finds a trip by ID and returns its summary, or null when not found/invalid.
    /// </summary>
    public async Task<TripSummaryDto?> Handle(GetTripByIdQuery q, CancellationToken ct)
    {
        if (!Guid.TryParse(q.TripId, out var idGuid)) return null;

        // Prefer repository projection for efficiency instead of loading full aggregate
        var dto = await _repo.GetSummaryAsync(new TripId(idGuid), ct);
        return dto;
    }
}