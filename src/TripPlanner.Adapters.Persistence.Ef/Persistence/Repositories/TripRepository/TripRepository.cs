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

    public Task<Guid> AddPlaceholderAsync(Guid tripId, string displayName, Guid createdByUserId, CancellationToken ct)
        => _writer.AddPlaceholderAsync(tripId, displayName, createdByUserId, ct);

    public Task<bool> ClaimPlaceholderAsync(string claimCode, Guid callerUserId, string? displayNameOverride, CancellationToken ct)
        => _writer.ClaimPlaceholderAsync(claimCode, callerUserId, displayNameOverride, ct);

    public Task<(Guid inviteId, string rawCode)> CreateInviteAsync(Guid tripId, Guid createdByUserId, TimeSpan? ttl, int? maxUses, CancellationToken ct)
        => _writer.CreateInviteAsync(tripId, createdByUserId, ttl, maxUses, ct);

    public Task<bool> RevokeInviteAsync(Guid tripId, Guid inviteId, CancellationToken ct)
        => _writer.RevokeInviteAsync(tripId, inviteId, ct);

    public Task<bool> JoinByCodeAsync(string code, Guid callerUserId, CancellationToken ct)
        => _writer.JoinByCodeAsync(code, callerUserId, ct);

    public Task<bool> CastDateVoteAsAsync(Guid tripId, Guid dateOptionId, Guid participantId, CancellationToken ct)
        => _writer.CastDateVoteAsAsync(tripId, dateOptionId, participantId, ct);

    public Task<bool> CastDestinationVoteAsAsync(Guid tripId, Guid destinationId, Guid participantId, CancellationToken ct)
        => _writer.CastDestinationVoteAsAsync(tripId, destinationId, participantId, ct);
}
