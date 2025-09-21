namespace TripPlanner.Core.Contracts.Contracts.Common.Participants;

public record ParticipantDto(
    string ParticipantId,
    string DisplayName,
    bool IsAnonymous,
    Guid UserId      
);