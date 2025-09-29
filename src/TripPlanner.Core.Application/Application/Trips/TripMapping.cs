using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Domain.Domain.Aggregates;

namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Centralized mapping between domain Trip and public DTOs.
/// Keep this focused and side-effect free.
/// </summary>
public static class TripMapping
{
    /// <summary>
    /// Maps a full Trip aggregate to a simple TripDto.
    /// </summary>
    public static TripDto ToDto(Trip t) =>
        new(t.Id.Value.ToString("D"), t.Name, t.OrganizerId.Value.ToString("D"));

    /// <summary>
    /// Maps a Trip aggregate to a TripSummaryDto. Some fields may be filled elsewhere later.
    /// </summary>
    public static TripSummaryDto ToSummary(Trip t) =>
        new(
            t.Id.Value.ToString("D"),
            t.Name,
            t.OrganizerId.Value.ToString("D"),
            "", // placeholder for description (not modeled yet)
            "", // placeholder for location (not modeled yet)
            false, // placeholder for completion state
            t.Participants.Select(p => p.Value.ToString("D")).ToList()
        );
}