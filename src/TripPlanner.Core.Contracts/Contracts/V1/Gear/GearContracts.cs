namespace TripPlanner.Core.Contracts.Contracts.V1.Gear;

/// <summary>
/// How an item is provisioned: either each participant brings their own, or a shared pool is sufficient.
/// </summary>
public enum GearProvisioning
{
    EACH,
    SHARED
}

/// <summary>
/// Assignment of a gear item (or part of its quantity) to a specific participant.
/// </summary>
/// <param name="AssignmentId">Identifier of this assignment entry.</param>
/// <param name="GearId">Identifier of the gear item being assigned.</param>
/// <param name="ParticipantId">Trip participant who takes responsibility.</param>
/// <param name="Quantity">How many units this participant will bring.</param>
/// <param name="CreatedAt">ISO timestamp when the assignment was made.</param>
public sealed record GearAssignmentDto(
    string AssignmentId,
    string GearId,
    string ParticipantId,
    int Quantity,
    string CreatedAt);

/// <summary>
/// A single gear item tracked for a trip with current assignments.
/// </summary>
/// <param name="GearId">Identifier of the gear item.</param>
/// <param name="TripId">Associated trip identifier.</param>
/// <param name="Group">Logical grouping (e.g., "Cooking", "Camping").</param>
/// <param name="Name">Human-readable item name.</param>
/// <param name="Provisioning">How the item is provided (each vs shared).</param>
/// <param name="NeededQuantity">Target quantity needed (null if not applicable).</param>
/// <param name="Tags">Optional labels for filtering.</param>
/// <param name="Assignments">Current participant assignments.</param>
public sealed record GearItemDto(
    string GearId,
    string TripId,
    string Group,
    string Name,
    GearProvisioning Provisioning,
    int? NeededQuantity,
    IReadOnlyList<string> Tags,
    IReadOnlyList<GearAssignmentDto> Assignments);

/// <summary>
/// Request to create a new gear item in a trip.
/// </summary>
public sealed record CreateGearItemRequest(
    string Group,
    string Name,
    GearProvisioning Provisioning,
    int? NeededQuantity,
    IReadOnlyList<string>? Tags);

/// <summary>
/// Request to update properties of an existing gear item. Nulls mean "no change".
/// </summary>
public sealed record UpdateGearItemRequest(
    string? Group,
    string? Name,
    GearProvisioning? Provisioning,
    int? NeededQuantity,
    IReadOnlyList<string>? Tags);

/// <summary>
/// Request to assign a gear item (or part) to a participant.
/// </summary>
public sealed record CreateGearAssignmentRequest(
    string ParticipantId,
    int? Quantity);

/// <summary>
/// Convenience payloads for importing multiple gear items at once.
/// </summary>
public sealed record BulkGearItem(string Name, GearProvisioning Provisioning, int? NeededQuantity, IReadOnlyList<string>? Tags);

public sealed record BulkGearGroup(string Group, IReadOnlyList<BulkGearItem> Items);

public sealed record BulkCreateGearRequest(IReadOnlyList<BulkGearGroup> Groups);
