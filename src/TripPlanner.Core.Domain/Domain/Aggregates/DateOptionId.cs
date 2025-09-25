namespace TripPlanner.Core.Domain.Domain.Aggregates;

public readonly record struct DateOptionId(Guid Value)
{
    public static DateOptionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}
