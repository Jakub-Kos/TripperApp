namespace TripPlanner.Core.Contracts.Contracts.Common.Participants;

/// <summary>
/// Canonical participant projection shared across API responses.
/// Represents either a real user or a placeholder invitee.
/// </summary>
public sealed class ParticipantInfoDto
{
    /// <summary>Stable identifier of the participant within the trip.</summary>
    public string ParticipantId { get; init; } = default!;

    /// <summary>Application user ID if linked; null for placeholders.</summary>
    public string? UserId { get; init; }

    /// <summary>Login/handle if available (often the email); null for placeholders.</summary>
    public string? Username { get; init; }

    /// <summary>Name shown to end users. For placeholders this is the label.</summary>
    public string DisplayName { get; init; } = default!;

    /// <summary>True if this entry represents a placeholder (not yet a real user).</summary>
    public bool IsPlaceholder { get; init; }

    /// <summary>True if the participant is the trip organizer.</summary>
    public bool IsOrganizer { get; init; }

    /// <summary>True if this participant corresponds to the current caller.</summary>
    public bool IsMe { get; init; }
}