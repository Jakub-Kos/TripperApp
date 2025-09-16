using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Domain.Domain.Aggregates;

public sealed class Trip
{
    private readonly HashSet<UserId> _participants = new();
    private readonly List<DateOption> _dateOptions = new();
    
    public TripId Id { get; private set; }
    public string Name { get; private set; }
    public UserId OrganizerId { get; private set; }
    public IReadOnlyCollection<UserId> Participants => _participants;
    public IReadOnlyCollection<DateOption> DateOptions => _dateOptions;
    
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
    
    public DateOption ProposeDate(DateOnly date)
    {
        if (_dateOptions.Any(d => d.Date == date)) return _dateOptions.First(d => d.Date == date);
        var option = new DateOption(DateOptionId.New(), date);
        _dateOptions.Add(option);
        return option;
    }

    public void CastVote(DateOptionId optId, UserId voter)
    {
        var opt = _dateOptions.FirstOrDefault(o => o.Id == optId)
                  ?? throw new InvalidOperationException("Date option not found.");
        opt.CastVote(voter);
    }

    public static Trip Rehydrate(TripId id, string name, UserId organizerId,
        IEnumerable<UserId> participants,
        IEnumerable<(DateOptionId optId, DateOnly date, IEnumerable<UserId> votes)> dateOptions)
    {
        var t = new Trip(id, name, organizerId);
        t._participants.UnionWith(participants);
        foreach (var (optId, date, votes) in dateOptions)
        {
            var opt = new DateOption(optId, date);
            foreach (var v in votes) opt.CastVote(v);
            t._dateOptions.Add(opt);
        }
        return t;
    }
}

public readonly record struct DateOptionId(Guid Value)
{
    public static DateOptionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

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