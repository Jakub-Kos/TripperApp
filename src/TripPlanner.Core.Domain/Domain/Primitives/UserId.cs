namespace TripPlanner.Core.Domain.Domain.Primitives;

/// <summary>
/// Strongly-typed identifier for a User in the domain.
/// </summary>
public readonly record struct UserId(Guid Value)
{
    /// <summary>Generates a new user identifier.</summary>
    public static UserId New() => new(Guid.NewGuid());

    /// <summary>Returns the canonical string representation.</summary>
    public override string ToString() => Value.ToString("D");
}