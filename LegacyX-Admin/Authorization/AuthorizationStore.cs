using System;
using System.Collections.Generic;
using System.Linq;

namespace LegacyX.Admin.Authorization;

public enum AuthorizationChange
{
    /// <summary>Nothing changed for this player.</summary>
    Unchanged,
    /// <summary>The player became staff.</summary>
    Granted,
    /// <summary>The player stays staff with a different role or expiry.</summary>
    Updated,
    /// <summary>The player is no longer staff.</summary>
    Revoked,
    /// <summary>The answer belonged to an older request (or the player left) and was ignored.</summary>
    Stale,
}

/// <summary>
/// In-memory authorization cache for the players on this server. Thread-safe: lookups finish on
/// thread-pool threads while the game thread reads. Only confirmed staff are kept; a grant is used
/// until its expiry or until it is <see cref="MaxAge"/> old without being re-confirmed, so a long
/// API outage ends in "no permissions" (fail closed), not in stale access.
/// </summary>
public sealed class AuthorizationStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, PlayerAuthorization> _grants = new();
    private readonly Dictionary<ulong, long> _latestTicket = new();
    private long _ticket;

    public AuthorizationStore(TimeSpan maxAge)
    {
        if (maxAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxAge));
        MaxAge = maxAge;
    }

    public TimeSpan MaxAge { get; }

    /// <summary>Starts a lookup for these players. Only the newest lookup per player may change it.</summary>
    public long BeginRequest(IEnumerable<ulong> steamIds)
    {
        lock (_gate)
        {
            var ticket = ++_ticket;
            foreach (var steamId in steamIds) _latestTicket[steamId] = ticket;
            return ticket;
        }
    }

    /// <summary>Applies a successful answer. <paramref name="answer"/> null = not staff.</summary>
    public AuthorizationChange Apply(ulong steamId, long ticket, PlayerAuthorization? answer, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!IsCurrent(steamId, ticket)) return AuthorizationChange.Stale;
            _grants.TryGetValue(steamId, out var previous);
            var hadGrant = previous != null && previous.IsValidAt(now, MaxAge);

            if (answer == null || answer.SteamId != steamId || !answer.IsValidAt(now, MaxAge))
                return _grants.Remove(steamId) ? AuthorizationChange.Revoked : AuthorizationChange.Unchanged;

            _grants[steamId] = answer;
            if (!hadGrant) return AuthorizationChange.Granted;
            return previous!.Role != answer.Role || previous.ExpiresAt != answer.ExpiresAt ? AuthorizationChange.Updated : AuthorizationChange.Unchanged;
        }
    }

    /// <summary>
    /// Applies a failed lookup. A new player gets nothing; an existing grant is kept only while it is
    /// still within its expiry and <see cref="MaxAge"/>.
    /// </summary>
    public AuthorizationChange ApplyFailure(ulong steamId, long ticket, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!IsCurrent(steamId, ticket)) return AuthorizationChange.Stale;
            if (!_grants.TryGetValue(steamId, out var previous)) return AuthorizationChange.Unchanged;
            if (previous.IsValidAt(now, MaxAge)) return AuthorizationChange.Unchanged;
            _grants.Remove(steamId);
            return AuthorizationChange.Revoked;
        }
    }

    /// <summary>Drops grants that expired or were not re-confirmed in time; returns whose they were.</summary>
    public IReadOnlyList<ulong> RemoveExpired(DateTimeOffset now)
    {
        lock (_gate)
        {
            var expired = _grants.Where(pair => !pair.Value.IsValidAt(now, MaxAge)).Select(pair => pair.Key).ToList();
            foreach (var steamId in expired) _grants.Remove(steamId);
            return expired;
        }
    }

    /// <summary>The player left: forget them and ignore any lookup still in flight.</summary>
    public bool Forget(ulong steamId)
    {
        lock (_gate)
        {
            _latestTicket.Remove(steamId);
            return _grants.Remove(steamId);
        }
    }

    /// <summary>The player's current grant, or null when they are not (or no longer) staff.</summary>
    public PlayerAuthorization? Get(ulong steamId, DateTimeOffset now)
    {
        lock (_gate)
        {
            return _grants.TryGetValue(steamId, out var grant) && grant.IsValidAt(now, MaxAge) ? grant : null;
        }
    }

    public IReadOnlyList<PlayerAuthorization> Snapshot(DateTimeOffset now)
    {
        lock (_gate)
        {
            return _grants.Values.Where(grant => grant.IsValidAt(now, MaxAge)).OrderByDescending(grant => grant.Role).ToList();
        }
    }

    public IReadOnlyList<ulong> Clear()
    {
        lock (_gate)
        {
            var all = _grants.Keys.ToList();
            _grants.Clear();
            _latestTicket.Clear();
            return all;
        }
    }

    // A player without a ticket left (Forget) after the lookup began: its answer is stale.
    private bool IsCurrent(ulong steamId, long ticket) => _latestTicket.TryGetValue(steamId, out var latest) && latest == ticket;
}
