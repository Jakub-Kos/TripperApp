namespace TripPlanner.Core.Domain.Domain.Primitives;

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}