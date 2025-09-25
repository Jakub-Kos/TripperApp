namespace TripPlanner.Core.Application.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}