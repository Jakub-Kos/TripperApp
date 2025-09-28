namespace TripPlanner.Core.Contracts.Contracts.V1.Gear;

public enum GearProvisioning
{
    EACH,
    SHARED
}

public sealed record GearAssignmentDto(
    string AssignmentId,
    string GearId,
    string ParticipantId,
    int Quantity,
    string CreatedAt);

public sealed record GearItemDto(
    string GearId,
    string TripId,
    string Group,
    string Name,
    GearProvisioning Provisioning,
    int? NeededQuantity,
    IReadOnlyList<string> Tags,
    IReadOnlyList<GearAssignmentDto> Assignments);

public sealed record CreateGearItemRequest(
    string Group,
    string Name,
    GearProvisioning Provisioning,
    int? NeededQuantity,
    IReadOnlyList<string>? Tags);

public sealed record UpdateGearItemRequest(
    string? Group,
    string? Name,
    GearProvisioning? Provisioning,
    int? NeededQuantity,
    IReadOnlyList<string>? Tags);

public sealed record CreateGearAssignmentRequest(
    string ParticipantId,
    int? Quantity);

public sealed record BulkGearItem(string Name, GearProvisioning Provisioning, int? NeededQuantity, IReadOnlyList<string>? Tags);

public sealed record BulkGearGroup(string Group, IReadOnlyList<BulkGearItem> Items);

public sealed record BulkCreateGearRequest(IReadOnlyList<BulkGearGroup> Groups);
