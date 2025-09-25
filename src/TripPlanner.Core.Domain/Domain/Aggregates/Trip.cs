using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Domain.Domain.Aggregates;

public sealed class Trip
{
    private readonly HashSet<UserId> _participants = new();
    private readonly List<DateOption> _dateOptions = new();
    private readonly List<DestinationProposal> _destinationProposals = new();
    public IReadOnlyCollection<DestinationProposal> DestinationProposals => _destinationProposals; 
    
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

    public static Trip Rehydrate(
        TripId id, 
        string name, 
        UserId organizerId,
        IEnumerable<UserId> participants,
        IEnumerable<(DateOptionId optId, DateOnly date, IEnumerable<UserId> votes)> dateOptions,
        IEnumerable<(DestinationId Id, string Title, string? Description, IEnumerable<string> ImageUrls, IEnumerable<UserId> Votes)> destinations)
    {
        var t = new Trip(id, name, organizerId);

        // participants
        if (participants is not null)
            t._participants.UnionWith(participants);

        // date options (+votes)
        if (dateOptions is not null)
        {
            foreach (var (optId, date, votes) in dateOptions)
            {
                var opt = new DateOption(optId, date);
                if (votes is not null)
                {
                    foreach (var v in votes)
                        opt.CastVote(v);
                }
                t._dateOptions.Add(opt);
            }
        }

        // destinations (+images + votes)
        if (destinations is not null)
        {
            foreach (var (destId, title, description, imageUrls, votes) in destinations)
            {
                var proposal = new DestinationProposal(destId, title, description, imageUrls ?? Array.Empty<string>());

                if (votes is not null)
                {
                    foreach (var v in votes)
                        proposal.AddVote(v);
                }

                t._destinationProposals.Add(proposal);
            }
        }

        return t;
    }

    public DestinationId ProposeDestination(string title, string? description, IEnumerable<string> imageUrls)
    {
        var p = new DestinationProposal(DestinationId.New(), title, description, imageUrls);
        _destinationProposals.Add(p);
        return p.Id;
    }

    public bool VoteDestination(DestinationId destinationId, UserId voter)
    {
        var p = _destinationProposals.FirstOrDefault(x => x.Id.Equals(destinationId));
        return p is not null && p.AddVote(voter);
    }
}