namespace TripPlanner.Core.Domain.Domain.Primitives;

/// <summary>
/// Strongly-typed identifier for a Destination proposal.
/// </summary>
public readonly record struct DestinationId(Guid Value)
{
    /// <summary>Generates a new destination identifier.</summary>
    public static DestinationId New() => new(Guid.NewGuid());

    /// <summary>Returns the canonical string representation.</summary>
    public override string ToString() => Value.ToString("D");
}