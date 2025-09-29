using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Domain.Domain.Aggregates;

/// <summary>
/// Candidate calendar date for the trip with simple upvote tracking.
/// </summary>
public sealed class DateOption
{
    // Votes are tracked as distinct users; duplicates are ignored by HashSet
    private readonly HashSet<UserId> _votes = new();

    /// <summary>Unique identifier of the date option.</summary>
    public DateOptionId Id { get; }

    /// <summary>The calendar date being proposed.</summary>
    public DateOnly Date { get; }

    /// <summary>Whether this option has been selected as the final choice.</summary>
    public bool IsChosen { get; private set; }

    /// <summary>Users who voted for this option.</summary>
    public IReadOnlyCollection<UserId> Votes => _votes;

    internal DateOption(DateOptionId id, DateOnly date, bool isChosen = false)
    {
        Id = id;
        Date = date;
        IsChosen = isChosen;
    }

    /// <summary>Marks this option as selected or unselected.</summary>
    internal void SetChosen(bool chosen) => IsChosen = chosen;

    /// <summary>Adds a vote from the given user; no-op if already voted.</summary>
    internal void CastVote(UserId user) => _votes.Add(user);
}
