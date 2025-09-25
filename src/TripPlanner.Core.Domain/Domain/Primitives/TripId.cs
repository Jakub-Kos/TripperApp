namespace TripPlanner.Core.Domain.Domain.Primitives;

public readonly record struct TripId(Guid Value)
{
    public static TripId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}