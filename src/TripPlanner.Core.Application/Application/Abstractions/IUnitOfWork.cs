namespace TripPlanner.Core.Application.Application.Abstractions;

/// <summary>
/// Transactional boundary for grouping repository writes.
/// Implementations typically wrap a DbContext or similar.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Flushes pending changes to the data store.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct);
}