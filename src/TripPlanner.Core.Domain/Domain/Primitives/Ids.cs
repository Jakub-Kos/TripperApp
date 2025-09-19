namespace TripPlanner.Core.Domain.Domain.Primitives;

public readonly record struct TripId(Guid Value)
{
    public static TripId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct DestinationId(Guid Value)
{
    public static DestinationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}