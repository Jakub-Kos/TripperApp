using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Common;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

public interface IUserRepository
{
    Task<UserRecord?> FindByEmail(string email, CancellationToken ct);
    Task<UserRecord?> FindById(Guid userId, CancellationToken ct);
    Task<UserRecord> Add(UserRecord user, CancellationToken ct);
    Task AddRefreshToken(RefreshTokenRecord token, CancellationToken ct);
    Task<RefreshTokenRecord?> FindRefreshToken(string token, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<UserRecord?> FindByEmail(string email, CancellationToken ct) =>
        db.Users
          .Include(u => u.RefreshTokens)
          .FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<UserRecord?> FindById(Guid userId, CancellationToken ct) =>
        db.Users
          .Include(u => u.RefreshTokens)
          .FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public async Task<UserRecord> Add(UserRecord user, CancellationToken ct)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task AddRefreshToken(RefreshTokenRecord token, CancellationToken ct)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(ct);
    }

    public Task<RefreshTokenRecord?> FindRefreshToken(string token, CancellationToken ct) =>
        db.RefreshTokens
          .Include(t => t.User)
          .FirstOrDefaultAsync(t => t.Token == token, ct);

    public Task SaveChanges(CancellationToken ct) => db.SaveChangesAsync(ct);
}