using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Common;
using TripPlanner.Core.Application.Application.Abstractions;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

public sealed class EfUserStore(AppDbContext db) : TripPlanner.Core.Application.Application.Abstractions.IUserRepository
{
    public async Task<UserInfo?> FindByEmail(string email, CancellationToken ct)
    {
        var u = await db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email == email, ct);
        return u is null ? null : new UserInfo(u.UserId, u.Email, u.DisplayName, u.PasswordHash);
    }

    public async Task<UserInfo?> FindById(Guid userId, CancellationToken ct)
    {
        var u = await db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);
        return u is null ? null : new UserInfo(u.UserId, u.Email, u.DisplayName, u.PasswordHash);
    }

    public async Task<Guid> Add(string email, string displayName, string passwordHash, CancellationToken ct)
    {
        var user = new UserRecord
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = passwordHash
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.UserId;
    }

    public async Task AddRefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct)
    {
        db.RefreshTokens.Add(new RefreshTokenRecord
        {
            UserId = userId,
            Token = tokenHash,
            ExpiresAt = expiresAt
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<RefreshTokenInfo?> FindRefreshToken(string tokenHash, CancellationToken ct)
    {
        var t = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == tokenHash, ct);
        if (t is null) return null;
        var user = t.User is null ? null : new UserInfo(t.User.UserId, t.User.Email, t.User.DisplayName, t.User.PasswordHash);
        return new RefreshTokenInfo(t.Token, t.UserId, t.ExpiresAt, t.RevokedAt, user);
    }

    public async Task RevokeRefreshToken(string tokenHash, DateTimeOffset revokedAt, CancellationToken ct)
    {
        var t = await db.RefreshTokens.FirstOrDefaultAsync(x => x.Token == tokenHash, ct);
        if (t is null) return;
        t.RevokedAt = revokedAt;
        await db.SaveChangesAsync(ct);
    }

    public Task SaveChanges(CancellationToken ct) => db.SaveChangesAsync(ct);
}