namespace TripPlanner.Core.Domain.Domain.Primitives;

public readonly record struct DestinationId(Guid Value)
{
    public static DestinationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}