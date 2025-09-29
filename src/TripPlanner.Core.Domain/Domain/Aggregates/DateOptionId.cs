namespace TripPlanner.Core.Domain.Domain.Aggregates;

/// <summary>
/// Strongly-typed identifier for a DateOption value object within a Trip aggregate.
/// </summary>
public readonly record struct DateOptionId(Guid Value)
{
    /// <summary>Generates a new date option identifier.</summary>
    public static DateOptionId New() => new(Guid.NewGuid());

    /// <summary>Returns the canonical string representation.</summary>
    public override string ToString() => Value.ToString("D");
}
