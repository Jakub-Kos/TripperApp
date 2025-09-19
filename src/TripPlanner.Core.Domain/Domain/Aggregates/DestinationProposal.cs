namespace TripPlanner.Core.Domain.Domain.Aggregates;

using TripPlanner.Core.Domain.Domain.Primitives;

public sealed class DestinationProposal
{
    public DestinationId Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public List<string> ImageUrls { get; } = new();
    private readonly HashSet<UserId> _votes = new();

    public int Votes => _votes.Count;
    public IReadOnlyCollection<UserId> VotesBy => _votes;

    public DestinationProposal(DestinationId id, string title, string? description, IEnumerable<string>? imageUrls = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        Id = id;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description!.Trim();
        if (imageUrls is not null) ImageUrls.AddRange(imageUrls.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public bool AddVote(UserId user) => _votes.Add(user);
}