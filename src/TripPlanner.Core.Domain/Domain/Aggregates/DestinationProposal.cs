namespace TripPlanner.Core.Domain.Domain.Aggregates;

using TripPlanner.Core.Domain.Domain.Primitives;

/// <summary>
/// A proposed destination for the trip, including optional images and community votes.
/// </summary>
public sealed class DestinationProposal
{
    // Track unique voters; duplicates are ignored
    private readonly HashSet<UserId> _votes = new();

    /// <summary>Unique identifier of the proposal.</summary>
    public DestinationId Id { get; }

    /// <summary>Short, human‑readable title of the destination.</summary>
    public string Title { get; }

    /// <summary>Optional longer description (markdown or plain text).</summary>
    public string? Description { get; }

    /// <summary>Associated image URLs; cleaned on construction.</summary>
    public List<string> ImageUrls { get; } = new();

    /// <summary>Whether this proposal has been selected.</summary>
    public bool IsChosen { get; private set; }

    /// <summary>Number of unique votes.</summary>
    public int Votes => _votes.Count;

    /// <summary>Users who voted for this proposal.</summary>
    public IReadOnlyCollection<UserId> VotesBy => _votes;

    public DestinationProposal(DestinationId id, string title, string? description, IEnumerable<string>? imageUrls = null, bool isChosen = false)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        Id = id;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description!.Trim();
        if (imageUrls is not null) ImageUrls.AddRange(imageUrls.Where(s => !string.IsNullOrWhiteSpace(s)));
        IsChosen = isChosen;
    }

    /// <summary>Marks this proposal as chosen or not chosen.</summary>
    public void SetChosen(bool chosen) => IsChosen = chosen;

    /// <summary>Adds a vote from the given user; returns false if user already voted.</summary>
    public bool AddVote(UserId user) => _votes.Add(user);
}