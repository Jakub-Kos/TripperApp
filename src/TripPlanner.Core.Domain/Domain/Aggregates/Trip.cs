using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Domain.Domain.Aggregates;

public sealed class Trip
{
    private readonly HashSet<UserId> _participants = new();

    public TripId Id { get; private set; }
    public string Name { get; private set; }
    public UserId OrganizerId { get; private set; }
    public IReadOnlyCollection<UserId> Participants => _participants;

    private Trip(TripId id, string name, UserId organizerId)
    {
        Id = id; Name = name; OrganizerId = organizerId;
    }

    public static Trip Create(string name, UserId organizer)
    {
        // TODO validate
        return new Trip(TripId.New(), name.Trim(), organizer);
    }

    public void AddParticipant(UserId user) => _participants.Add(user);
    
    public static Trip Rehydrate(TripId id, string name, UserId organizerId, IEnumerable<UserId> participants)
    {
        var t = new Trip(id, name, organizerId);
        t._participants.UnionWith(participants);
        return t;
    }
}