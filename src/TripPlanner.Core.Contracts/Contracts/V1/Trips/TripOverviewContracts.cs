using System;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.Common.Participants;

namespace TripPlanner.Core.Contracts.Contracts.V1.Trips;

/// <summary>
/// Overview of a trip including basic metadata and participants.
/// Intended for the main "overview" screen where full details are unnecessary.
/// </summary>
/// <param name="TripId">Identifier of the trip.</param>
/// <param name="Name">Display name of the trip.</param>
/// <param name="DescriptionMarkdown">Rich description in Markdown format.</param>
/// <param name="CreatedAt">UTC timestamp when the trip was created.</param>
/// <param name="Participants">Participants shown with organizer and placeholder flags.</param>
public sealed record TripOverviewDto(
    string TripId,
    string Name,
    string DescriptionMarkdown,
    DateTimeOffset CreatedAt,
    ParticipantInfoDto[] Participants
);

/// <summary>
/// Response payload for fetching a trip overview.
/// </summary>
/// <param name="Trip">The trip overview.</param>
public sealed record GetTripOverviewResponse(TripOverviewDto Trip);

// Requests

/// <summary>
/// PATCH body to update the trip's Markdown description.
/// </summary>
/// <param name="DescriptionMarkdown">Full replacement Markdown text.</param>
public sealed record UpdateTripDescriptionRequest(string DescriptionMarkdown);

/// <summary>
/// Request to add a participant to the trip.
/// </summary>
/// <param name="UserId">If provided, links an existing registered user; if null, creates a placeholder.</param>
/// <param name="DisplayName">Display name for the participant or placeholder.</param>
public sealed record AddParticipantRequest(Guid? UserId, string DisplayName);