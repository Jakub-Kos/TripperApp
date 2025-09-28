namespace TripPlanner.Core.Contracts.Contracts.Common.Participants;

// Legacy DTO retained for compatibility in clients; server may return a different shape.
public record ParticipantDto(
    string ParticipantId,
    string DisplayName,
    bool IsAnonymous,
    Guid UserId
);

// New richer DTO for participant info
public sealed class ParticipantInfoDto
{
    public string ParticipantId { get; init; } = default!;
    public string DisplayName { get; init; } = default!; // placeholder or user-facing name
    public bool IsPlaceholder { get; init; }
    public bool IsOrganizer { get; init; }
    public bool IsMe { get; init; }
    public string? UserId { get; init; } // null for placeholders
    public string? Username { get; init; } // using Email as login/handle here
}