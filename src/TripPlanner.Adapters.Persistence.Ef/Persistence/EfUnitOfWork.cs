using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public EfUnitOfWork(AppDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}