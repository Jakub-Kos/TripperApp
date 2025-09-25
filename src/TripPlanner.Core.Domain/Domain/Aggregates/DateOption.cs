using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Domain.Domain.Aggregates;

public sealed class DateOption
{
    private readonly HashSet<UserId> _votes = new();
    public DateOptionId Id { get; }
    public DateOnly Date { get; }

    public IReadOnlyCollection<UserId> Votes => _votes;

    internal DateOption(DateOptionId id, DateOnly date)
    {
        Id = id; Date = date;
    }

    internal void CastVote(UserId user) => _votes.Add(user);
}
