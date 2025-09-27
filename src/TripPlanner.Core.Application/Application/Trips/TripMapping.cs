using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Domain.Domain.Aggregates;

namespace TripPlanner.Core.Application.Application.Trips;

public static class TripMapping
{
    public static TripDto ToDto(Trip t) =>
        new(t.Id.Value.ToString("D"), t.Name, t.OrganizerId.Value.ToString("D"));

    public static TripSummaryDto ToSummary(Trip t) =>
        new(
            t.Id.Value.ToString("D"),
            t.Name,
            t.OrganizerId.Value.ToString("D"),
            "",
            "",
            false,
            t.Participants.Select(p => p.Value.ToString("D")).ToList()
        );
}