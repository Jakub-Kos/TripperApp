using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Db;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Destination;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

internal sealed class TripWriter
{
    private readonly AppDbContext _db;
    public TripWriter(AppDbContext db) => _db = db;

    // Participants
    /// <summary>
    /// Add a registered user as a participant (idempotent by (TripId, UserId)). Returns true if ensured present.
    /// </summary>
    public async Task<bool> AddParticipant(TripId tripId, UserId userId, CancellationToken ct)
    {
        var tripExists = await _db.Trips.AnyAsync(t => t.TripId == tripId.Value, ct);
        if (!tripExists) return false;

        var already = await _db.Participants.AnyAsync(p => p.TripId == tripId.Value && p.UserId == userId.Value, ct);
        if (already) return true;

        var display = await GetUserDisplayNameOrFallback(userId.Value, ct);

        _db.Participants.Add(new ParticipantRecord
        {
            TripId = tripId.Value,
            ParticipantId = Guid.NewGuid(),
            UserId = userId.Value,
            IsPlaceholder = false,
            DisplayName = display,
            CreatedByUserId = userId.Value
        });

        return true; // UoW will save
    }

    /// <summary>
    /// Add a named placeholder participant (saves immediately because UI often relies on the new id).
    /// </summary>
    public async Task<Guid> AddPlaceholderAsync(Guid tripId, string displayName, Guid createdByUserId, CancellationToken ct)
    {
        var tripExists = await _db.Trips.AnyAsync(t => t.TripId == tripId, ct);
        if (!tripExists) throw new InvalidOperationException("Trip not found.");

        var name = string.IsNullOrWhiteSpace(displayName) ? "Guest" : displayName.Trim();

        var p = new ParticipantRecord
        {
            TripId = tripId,
            ParticipantId = Guid.NewGuid(),
            UserId = null,
            IsPlaceholder = true,
            DisplayName = name,
            CreatedByUserId = createdByUserId
        };

        _db.Participants.Add(p);
        await _db.SaveChangesAsync(ct);
        return p.ParticipantId;
    }

    // Date Options
    /// <summary>Proposes a new date option (idempotent by (TripId, DateIso)).</summary>
    public async Task<DateOptionId> ProposeDateOption(TripId tripId, DateOnly date, CancellationToken ct)
    {
        var exists = await _db.Trips.AnyAsync(t => t.TripId == tripId.Value, ct);
        if (!exists) throw new InvalidOperationException("Trip not found.");

        var iso = date.ToString("yyyy-MM-dd");
        var dup = await _db.DateOptions.AnyAsync(o => o.TripId == tripId.Value && o.DateIso == iso, ct);
        if (dup)
        {
            var existing = await _db.DateOptions.FirstAsync(o => o.TripId == tripId.Value && o.DateIso == iso, ct);
            return new DateOptionId(existing.DateOptionId);
        }

        var id = DateOptionId.New();
        _db.DateOptions.Add(new DateOptionRecord { DateOptionId = id.Value, TripId = tripId.Value, DateIso = iso });
        return id; // UoW will save
    }

    // Voting (participant-based; supports self- and proxy- voting)

    /// <summary>Cast a date vote as a participant (idempotent by (DateOptionId, ParticipantId)). Saves immediately.</summary>
    public async Task<bool> CastDateVoteAsAsync(Guid tripId, Guid dateOptionId, Guid participantId, CancellationToken ct)
    {
        var participant = await _db.Participants.FirstOrDefaultAsync(p => p.TripId == tripId && p.ParticipantId == participantId, ct);
        if (participant is null) return false;

        var option = await _db.DateOptions.FirstOrDefaultAsync(o => o.DateOptionId == dateOptionId && o.TripId == tripId, ct);
        if (option is null) return false;

        var dup = await _db.DateVotes.AnyAsync(v => v.DateOptionId == dateOptionId && v.ParticipantId == participantId, ct);
        if (dup) return true;

        _db.DateVotes.Add(new DateVoteRecord
        {
            DateOptionId = dateOptionId,
            ParticipantId = participantId
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Cast a destination vote as a participant (idempotent by (DestinationId, ParticipantId)). Saves immediately.</summary>
    public async Task<bool> CastDestinationVoteAsAsync(Guid tripId, Guid destinationId, Guid participantId, CancellationToken ct)
    {
        var participant = await _db.Participants.FirstOrDefaultAsync(p => p.TripId == tripId && p.ParticipantId == participantId, ct);
        if (participant is null) return false;

        var dest = await _db.Destinations.FirstOrDefaultAsync(d => d.DestinationId == destinationId && d.TripId == tripId, ct);
        if (dest is null) return false;

        var dup = await _db.DestinationVotes.AnyAsync(v => v.DestinationId == destinationId && v.ParticipantId == participantId, ct);
        if (dup) return true;

        _db.DestinationVotes.Add(new DestinationVoteRecord
        {
            DestinationId = destinationId,
            ParticipantId = participantId
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Invites (trip join)
    public async Task<(Guid inviteId, string rawCode)> CreateInviteAsync(Guid tripId, Guid createdByUserId, TimeSpan? ttl, int? maxUses, CancellationToken ct)
    {
        var tripExists = await _db.Trips.AnyAsync(t => t.TripId == tripId, ct);
        if (!tripExists) throw new InvalidOperationException("Trip not found.");

        var code = GenerateFriendlyCode(10); // Crockford base32, 10 chars
        var hash = Hash(code);
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromDays(1));

        var rec = new TripInviteRecord
        {
            TripId = tripId,
            CodeHash = hash,
            ExpiresAt = expiresAt,
            MaxUses = Math.Max(1, maxUses ?? 10),
            Uses = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = createdByUserId
        };

        _db.TripInvites.Add(rec);
        await _db.SaveChangesAsync(ct);
        return (rec.InviteId, code);
    }

    public async Task<bool> RevokeInviteAsync(Guid tripId, Guid inviteId, CancellationToken ct)
    {
        var rec = await _db.TripInvites.FirstOrDefaultAsync(i => i.InviteId == inviteId && i.TripId == tripId, ct);
        if (rec is null) return false;
        if (rec.RevokedAt is not null) return true;

        rec.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> JoinByCodeAsync(string code, Guid callerUserId, CancellationToken ct)
    {
        var norm = NormalizeCode(code);
        var hash = Hash(norm);
        var now = DateTimeOffset.UtcNow;

        var invite = await _db.TripInvites
            .AsTracking()
            .FirstOrDefaultAsync(i => i.CodeHash == hash && i.RevokedAt == null && i.ExpiresAt > now, ct);

        if (invite is null) return false;

        var already = await _db.Participants.AnyAsync(p => p.TripId == invite.TripId && p.UserId == callerUserId, ct);
        if (already) return true;

        var display = await GetUserDisplayNameOrFallback(callerUserId, ct);

        _db.Participants.Add(new ParticipantRecord
        {
            TripId = invite.TripId,
            ParticipantId = Guid.NewGuid(),
            UserId = callerUserId,
            IsPlaceholder = false,
            DisplayName = display,
            CreatedByUserId = callerUserId
        });

        invite.Uses += 1;
        if (invite.Uses >= invite.MaxUses) invite.RevokedAt = now;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Placeholder claim codes
    /// <summary>Issue a one-time claim code for a specific placeholder participant. Saves and returns plaintext.</summary>
    public async Task<(Guid claimId, string code, DateTimeOffset expiresAt)> CreatePlaceholderClaimAsync(Guid tripId, Guid participantId, Guid createdByUserId, TimeSpan? ttl, CancellationToken ct)
    {
        var placeholder = await _db.Participants.FirstOrDefaultAsync(p =>
            p.TripId == tripId && p.ParticipantId == participantId && p.UserId == null, ct);

        if (placeholder is null) throw new InvalidOperationException("Placeholder not found.");

        var code = GenerateFriendlyCode(10);
        var hash = Hash(code);
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromDays(1));

        var rec = new PlaceholderClaimRecord
        {
            TripId = tripId,
            ParticipantId = participantId,
            CodeHash = hash,
            ExpiresAt = expiresAt,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.PlaceholderClaims.Add(rec);
        await _db.SaveChangesAsync(ct);
        return (rec.ClaimId, code, expiresAt);
    }

    /// <summary>Claim a placeholder using a claim code. Does not merge with any existing participant; if user already has a participant, fails.</summary>
    public async Task<bool> ClaimPlaceholderAsync(string claimCode, Guid callerUserId, string? displayNameOverride, CancellationToken ct)
    {
        var norm = NormalizeCode(claimCode);
        var hash = Hash(norm);
        var now = DateTimeOffset.UtcNow;

        var claim = await _db.PlaceholderClaims
            .AsTracking()
            .FirstOrDefaultAsync(c => c.CodeHash == hash && c.RevokedAt == null && c.ExpiresAt > now, ct);

        if (claim is null) return false;

        var placeholder = await _db.Participants
            .FirstOrDefaultAsync(p => p.TripId == claim.TripId && p.ParticipantId == claim.ParticipantId && p.UserId == null, ct);

        if (placeholder is null) return false;

        var existingForUser = await _db.Participants
            .FirstOrDefaultAsync(p => p.TripId == claim.TripId && p.UserId == callerUserId, ct);

        if (existingForUser is not null && existingForUser.ParticipantId != placeholder.ParticipantId)
        {
            // Do not merge; disallow multiple participants for the same user via claim.
            return false;
        }

        placeholder.UserId = callerUserId;
        placeholder.IsPlaceholder = false;
        placeholder.ClaimedAt = now;
        if (!string.IsNullOrWhiteSpace(displayNameOverride))
            placeholder.DisplayName = displayNameOverride!.Trim();
        else
            placeholder.DisplayName = await GetUserDisplayNameOrFallback(callerUserId, ct);

        claim.RevokedAt = now; // one-time
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Helpers
    private static string NormalizeCode(string raw)
        => new(raw.Trim().ToUpperInvariant().Where(ch => ch != '-' && ch != ' ').ToArray());

    private static string Hash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static string GenerateFriendlyCode(int length)
    {
        // Crockford Base32 (no I, L, O, U)
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var numBytes = (int)Math.Ceiling(length * 5 / 8.0);
        Span<byte> buffer = stackalloc byte[numBytes];
        RandomNumberGenerator.Fill(buffer);

        var bits = 0;
        var value = 0;
        var output = new StringBuilder(length);
        foreach (var b in buffer)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5 && output.Length < length)
            {
                var index = (value >> (bits - 5)) & 31;
                output.Append(alphabet[index]);
                bits -= 5;
            }
        }
        while (output.Length < length)
            output.Append(alphabet[RandomNumberGenerator.GetInt32(32)]);
        return output.ToString();
    }

    private async Task<string> GetUserDisplayNameOrFallback(Guid userId, CancellationToken ct)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        return u?.DisplayName ?? "User";
    }
}
