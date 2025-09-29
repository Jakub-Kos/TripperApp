using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Domain.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a trip. Holds metadata, participants, date options, and destination proposals.
/// </summary>
public sealed class Trip
{
    // Backing collections (mutated internally only)
    private readonly HashSet<UserId> _participants = new();
    private readonly List<DateOption> _dateOptions = new();
    private readonly List<DestinationProposal> _destinationProposals = new();

    /// <summary>Trip identifier.</summary>
    public TripId Id { get; private set; }

    /// <summary>Display name of the trip.</summary>
    public string Name { get; private set; }

    /// <summary>User who created/organizes the trip.</summary>
    public UserId OrganizerId { get; private set; }

    /// <summary>Participants invited to or attending the trip.</summary>
    public IReadOnlyCollection<UserId> Participants => _participants;

    /// <summary>All proposed date options with votes.</summary>
    public IReadOnlyCollection<DateOption> DateOptions => _dateOptions;

    /// <summary>All proposed destinations with votes.</summary>
    public IReadOnlyCollection<DestinationProposal> DestinationProposals => _destinationProposals;

    /// <summary>
    /// Date range (inclusive) for when the trip should take place. If nulls, no range is defined yet.
    /// </summary>
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }

    private Trip(TripId id, string name, UserId organizerId)
    {
        Id = id;
        Name = name;
        OrganizerId = organizerId;
    }

    /// <summary>Factory for creating a new trip.</summary>
    public static Trip Create(string name, UserId organizer)
    {
        // TODO: add richer validation as needed
        return new Trip(TripId.New(), name.Trim(), organizer);
    }

    /// <summary>Rehydrates a trip from persisted state including participants, date options and destinations.</summary>
    public static Trip Rehydrate(
        TripId id,
        string name,
        UserId organizerId,
        IEnumerable<UserId> participants,
        IEnumerable<(DateOptionId optId, DateOnly date, IEnumerable<UserId> votes, bool isChosen)> dateOptions,
        IEnumerable<(DestinationId Id, string Title, string? Description, IEnumerable<string> ImageUrls, IEnumerable<UserId> Votes, bool IsChosen)> destinations,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        var t = new Trip(id, name, organizerId)
        {
            StartDate = startDate,
            EndDate = endDate
        };

        // participants
        if (participants is not null)
            t._participants.UnionWith(participants);

        // date options (+votes)
        if (dateOptions is not null)
        {
            foreach (var (optId, date, votes, isChosen) in dateOptions)
            {
                var opt = new DateOption(optId, date, isChosen);
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
            foreach (var (destId, title, description, imageUrls, votes, isChosen) in destinations)
            {
                var proposal = new DestinationProposal(destId, title, description, imageUrls ?? Array.Empty<string>(), isChosen);

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

    // Participants -----------------------------------------------------------

    /// <summary>Adds a participant to the trip.</summary>
    public void AddParticipant(UserId user) => _participants.Add(user);

    // Date options -----------------------------------------------------------

    /// <summary>Sets the valid date range for the trip (inclusive).</summary>
    public void SetDateRange(DateOnly start, DateOnly end)
    {
        if (end < start) throw new ArgumentException("End date must be on or after start date.");
        StartDate = start;
        EndDate = end;
    }

    /// <summary>
    /// Creates the date option for the specified date if missing and casts a vote from the given user.
    /// Enforces the configured date range if present.
    /// </summary>
    public DateOption VoteOnDate(DateOnly date, UserId voter)
    {
        // If a range is set, enforce it
        if (StartDate is not null && EndDate is not null)
        {
            if (date < StartDate.Value || date > EndDate.Value)
                throw new InvalidOperationException("Date is outside the allowed trip range.");
        }
        var opt = _dateOptions.FirstOrDefault(d => d.Date == date);
        if (opt is null)
        {
            opt = new DateOption(DateOptionId.New(), date);
            _dateOptions.Add(opt);
        }
        opt.CastVote(voter);
        return opt;
    }

    /// <summary>Creates or returns an existing date option without casting a vote.</summary>
    public DateOption ProposeDate(DateOnly date)
    {
        var opt = _dateOptions.FirstOrDefault(d => d.Date == date);
        if (opt is null)
        {
            opt = new DateOption(DateOptionId.New(), date);
            _dateOptions.Add(opt);
        }
        return opt;
    }

    /// <summary>Casts a vote for an existing date option by its identifier.</summary>
    public void CastVote(DateOptionId optId, UserId voter)
    {
        var opt = _dateOptions.FirstOrDefault(o => o.Id == optId)
                  ?? throw new InvalidOperationException("Date option not found.");
        opt.CastVote(voter);
    }

    // Destinations -----------------------------------------------------------

    /// <summary>Creates a new destination proposal and returns its identifier.</summary>
    public DestinationId ProposeDestination(string title, string? description, IEnumerable<string> imageUrls)
    {
        var p = new DestinationProposal(DestinationId.New(), title, description, imageUrls);
        _destinationProposals.Add(p);
        return p.Id;
    }

    /// <summary>Adds a vote for the given destination proposal; returns false if already voted.</summary>
    public bool VoteDestination(DestinationId destinationId, UserId voter)
    {
        var p = _destinationProposals.FirstOrDefault(x => x.Id.Equals(destinationId));
        return p is not null && p.AddVote(voter);
    }
}