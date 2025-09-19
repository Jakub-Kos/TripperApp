using System.Collections.Concurrent;
using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Adapters.Persistence.InMemory.Persistence;

public sealed class InMemoryTripRepository : ITripRepository, IUnitOfWork
{
    // In-memory aggregate store
    private readonly ConcurrentDictionary<TripId, Trip> _store = new();

    // --- ITripRepository ---

    public Task<Trip?> FindByIdAsync(TripId id, CancellationToken ct = default)
        => Get(id, ct);
    public Task<Trip?> Get(TripId id, CancellationToken ct)
    {
        _store.TryGetValue(id, out var trip);
        return Task.FromResult(trip);
    }

    public Task AddAsync(Trip trip, CancellationToken ct = default)
    {
        _store[trip.Id] = trip;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Trip>> ListAsync(int skip, int take, CancellationToken ct = default)
    {
        var list = _store.Values
            .OrderBy(t => t.Name)
            .Skip(skip).Take(take)
            .ToList();
        return Task.FromResult<IReadOnlyList<Trip>>(list);
    }

    // Persist any changes on the aggregate (no-op semantics for in-memory)
    public Task UpdateAsync(Trip trip, CancellationToken ct = default)
    {
        _store[trip.Id] = trip;
        return Task.CompletedTask;
    }

    // --- Targeted mutations (mirror EF repo behavior) ---

    public Task<bool> AddParticipant(TripId tripId, UserId userId, CancellationToken ct)
    {
        if (!_store.TryGetValue(tripId, out var trip))
            return Task.FromResult(false);

        // idempotent: if already present, succeed
        if (trip.Participants.Contains(userId))
            return Task.FromResult(true);

        // Rehydrate a new aggregate with added participant
        var participants = trip.Participants.Append(userId);
        var options = trip.DateOptions.Select(o => (o.Id, o.Date, (IEnumerable<UserId>)o.Votes));
        var destinations = trip.DestinationProposals.Select(p =>
            (p.Id, p.Title, p.Description, (IEnumerable<string>)p.ImageUrls, (IEnumerable<UserId>)p.VotesBy));

        var updated = Trip.Rehydrate(trip.Id, trip.Name, trip.OrganizerId, participants, options, destinations);
        _store[tripId] = updated;
        return Task.FromResult(true);
    }

    public Task<DateOptionId> ProposeDateOption(TripId tripId, DateOnly date, CancellationToken ct)
    {
        if (!_store.TryGetValue(tripId, out var trip))
            throw new InvalidOperationException("Trip not found.");

        var iso = date.ToString("yyyy-MM-dd");
        // duplicate check by date value
        var existing = trip.DateOptions.FirstOrDefault(o => o.Date.ToString("yyyy-MM-dd") == iso);
        if (existing.Id.Value != Guid.Empty)
            return Task.FromResult(existing.Id);

        var id = DateOptionId.New();

        var participants = trip.Participants;
        var options = trip.DateOptions
            .Select(o => (o.Id, o.Date, (IEnumerable<UserId>)o.Votes))
            .Append((id, date, Enumerable.Empty<UserId>()));

        var destinations = trip.DestinationProposals.Select(p =>
            (p.Id, p.Title, p.Description, (IEnumerable<string>)p.ImageUrls, (IEnumerable<UserId>)p.VotesBy));

        var updated = Trip.Rehydrate(trip.Id, trip.Name, trip.OrganizerId, participants, options, destinations);
        _store[tripId] = updated;

        return Task.FromResult(id);
    }

    public Task<bool> CastVote(TripId tripId, DateOptionId dateOptionId, UserId userId, CancellationToken ct)
    {
        if (!_store.TryGetValue(tripId, out var trip))
            return Task.FromResult(false);

        var option = trip.DateOptions.FirstOrDefault(o => o.Id == dateOptionId);
        if (option.Id.Value == Guid.Empty)
            return Task.FromResult(false);

        // idempotent
        if (option.Votes.Contains(userId))
            return Task.FromResult(true);

        var participants = trip.Participants;

        var options = trip.DateOptions.Select(o =>
        {
            if (o.Id == dateOptionId)
            {
                var newVotes = o.Votes.Append(userId);
                return (o.Id, o.Date, (IEnumerable<UserId>)newVotes);
            }
            return (o.Id, o.Date, (IEnumerable<UserId>)o.Votes);
        });

        var destinations = trip.DestinationProposals.Select(p =>
            (p.Id, p.Title, p.Description, (IEnumerable<string>)p.ImageUrls, (IEnumerable<UserId>)p.VotesBy));

        var updated = Trip.Rehydrate(trip.Id, trip.Name, trip.OrganizerId, participants, options, destinations);
        _store[tripId] = updated;

        return Task.FromResult(true);
    }

    // --- IUnitOfWork ---

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // In-memory store: nothing to flush
        return Task.FromResult(0);
    }
}
