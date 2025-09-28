using System;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.Common.Participants;

namespace TripPlanner.Core.Contracts.Contracts.V1.Trips;

public record TripOverviewDto(
    string TripId,
    string Name,
    string DescriptionMarkdown,
    DateTimeOffset CreatedAt,
    ParticipantInfoDto[] Participants
);

public record GetTripOverviewResponse(TripOverviewDto Trip);

// PATCH body to update markdown
public record UpdateTripDescriptionRequest(string DescriptionMarkdown);

// POST body to add participant:
// - If UserId provided => registered user
// - If null => anonymous with provided DisplayName
public record AddParticipantRequest(Guid? UserId, string DisplayName);