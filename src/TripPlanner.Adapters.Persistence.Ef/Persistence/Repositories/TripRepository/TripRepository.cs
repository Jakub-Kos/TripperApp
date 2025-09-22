using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

public sealed class TripRepository : ITripRepository
{
    private readonly TripAggregateStore _store;
    private readonly TripQueries _queries;
    private readonly TripWriter _writer;

    public TripRepository(AppDbContext db)
    {
        _store = new TripAggregateStore(db);
        _queries = new TripQueries(db);
        _writer = new TripWriter(db);
    }

    public Task<Trip?> Get(TripId id, CancellationToken ct) => _store.Get(id, ct);

    public Task<Trip?> FindByIdAsync(TripId id, CancellationToken ct = default) => Get(id, ct);

    public Task AddAsync(Trip trip, CancellationToken ct) => _store.AddAsync(trip, ct);

    public Task UpdateAsync(Trip trip, CancellationToken ct = default) => _store.UpdateAsync(trip, ct);

    public Task<IReadOnlyList<Trip>> ListAsync(int skip, int take, CancellationToken ct) => _queries.ListAsync(skip, take, ct);

    public Task<bool> AddParticipant(TripId tripId, UserId userId, CancellationToken ct) => _writer.AddParticipant(tripId, userId, ct);

    public Task<DateOptionId> ProposeDateOption(TripId tripId, DateOnly date, CancellationToken ct) => _writer.ProposeDateOption(tripId, date, ct);

    public Task<bool> CastVote(TripId tripId, DateOptionId dateOptionId, UserId userId, CancellationToken ct) => _writer.CastVote(tripId, dateOptionId, userId, ct);
    
    public Task AddAnonymousAsync(Guid tripId, string displayName, CancellationToken ct) => _writer.AddAnonymousAsync(tripId, displayName, ct);
}