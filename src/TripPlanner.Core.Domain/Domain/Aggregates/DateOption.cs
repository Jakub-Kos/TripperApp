using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Domain.Domain.Aggregates;

public sealed class DateOption
{
    private readonly HashSet<UserId> _votes = new();
    public DateOptionId Id { get; }
    public DateOnly Date { get; }
    public bool IsChosen { get; private set; }

    public IReadOnlyCollection<UserId> Votes => _votes;

    internal DateOption(DateOptionId id, DateOnly date, bool isChosen = false)
    {
        Id = id; Date = date; IsChosen = isChosen;
    }

    internal void SetChosen(bool chosen) => IsChosen = chosen;
    internal void CastVote(UserId user) => _votes.Add(user);
}
