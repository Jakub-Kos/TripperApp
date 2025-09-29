namespace TripPlanner.Core.Domain.Domain.Primitives;

/// <summary>
/// Strongly-typed identifier for a Trip aggregate.
/// </summary>
public readonly record struct TripId(Guid Value)
{
    /// <summary>Generates a new trip identifier.</summary>
    public static TripId New() => new(Guid.NewGuid());

    /// <summary>Returns the canonical string representation.</summary>
    public override string ToString() => Value.ToString("D");
}