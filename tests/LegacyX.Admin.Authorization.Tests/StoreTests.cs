using System;
using System.Linq;
using System.Threading.Tasks;
using LegacyX.Admin.Authorization;
using Xunit;
using static LegacyX.Admin.Authorization.Tests.ParserTests;

namespace LegacyX.Admin.Authorization.Tests;

public class StoreTests
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(180);

    private static PlayerAuthorization Grant(ulong steamId, StaffRole role, DateTimeOffset checkedAt, DateTimeOffset? expiresAt = null) => new(steamId, role, expiresAt, checkedAt);

    [Fact]
    public void Grants_updates_keeps_and_revokes()
    {
        var store = new AuthorizationStore(MaxAge);
        Assert.Equal(AuthorizationChange.Granted, store.Apply(A, store.BeginRequest(new[] { A }), Grant(A, StaffRole.Admin, Now), Now));
        Assert.Equal(StaffRole.Admin, store.Get(A, Now)!.Role);

        Assert.Equal(AuthorizationChange.Unchanged, store.Apply(A, store.BeginRequest(new[] { A }), Grant(A, StaffRole.Admin, Now.AddSeconds(60)), Now.AddSeconds(60)));
        Assert.Equal(AuthorizationChange.Updated, store.Apply(A, store.BeginRequest(new[] { A }), Grant(A, StaffRole.Manager, Now.AddSeconds(120)), Now.AddSeconds(120)));
        Assert.Equal(StaffRole.Manager, store.Get(A, Now.AddSeconds(120))!.Role);

        // Revoked on the website: the next answer says "not staff".
        Assert.Equal(AuthorizationChange.Revoked, store.Apply(A, store.BeginRequest(new[] { A }), null, Now.AddSeconds(180)));
        Assert.Null(store.Get(A, Now.AddSeconds(180)));
    }

    [Fact]
    public void A_player_answer_never_becomes_a_grant()
    {
        var store = new AuthorizationStore(MaxAge);
        Assert.Equal(AuthorizationChange.Unchanged, store.Apply(A, store.BeginRequest(new[] { A }), Grant(A, StaffRole.Player, Now), Now));
        Assert.Null(store.Get(A, Now));
        // An answer for someone else can't grant this player.
        Assert.Equal(AuthorizationChange.Unchanged, store.Apply(A, store.BeginRequest(new[] { A }), Grant(B, StaffRole.Owner, Now), Now));
        Assert.Null(store.Get(A, Now));
    }

    [Fact]
    public void Api_failure_grants_nothing_to_a_new_player()
    {
        var store = new AuthorizationStore(MaxAge);
        Assert.Equal(AuthorizationChange.Unchanged, store.ApplyFailure(A, store.BeginRequest(new[] { A }), Now));
        Assert.Null(store.Get(A, Now));
    }

    [Fact]
    public void Api_failure_keeps_a_recent_grant_but_only_until_max_age()
    {
        var store = new AuthorizationStore(MaxAge);
        store.Apply(A, store.BeginRequest(new[] { A }), Grant(A, StaffRole.Admin, Now), Now);

        Assert.Equal(AuthorizationChange.Unchanged, store.ApplyFailure(A, store.BeginRequest(new[] { A }), Now.AddSeconds(60)));
        Assert.NotNull(store.Get(A, Now.AddSeconds(60)));

        Assert.Equal(AuthorizationChange.Revoked, store.ApplyFailure(A, store.BeginRequest(new[] { A }), Now.AddSeconds(181)));
        Assert.Null(store.Get(A, Now.AddSeconds(181)));
    }

    [Fact]
    public void Expired_assignments_are_removed_even_without_a_refresh()
    {
        var store = new AuthorizationStore(MaxAge);
        store.Apply(A, store.BeginRequest(new[] { A }), Grant(A, StaffRole.Staff, Now, Now.AddSeconds(30)), Now);
        store.Apply(B, store.BeginRequest(new[] { B }), Grant(B, StaffRole.Admin, Now), Now);

        Assert.Empty(store.RemoveExpired(Now.AddSeconds(29)));
        Assert.Equal(new[] { A }, store.RemoveExpired(Now.AddSeconds(30)));
        Assert.Null(store.Get(A, Now.AddSeconds(30)));
        Assert.NotNull(store.Get(B, Now.AddSeconds(30)));
        // Not re-confirmed within max age: removed as well.
        Assert.Equal(new[] { B }, store.RemoveExpired(Now.AddSeconds(180)));
    }

    [Fact]
    public void An_older_answer_arriving_late_is_ignored()
    {
        var store = new AuthorizationStore(MaxAge);
        var older = store.BeginRequest(new[] { A });
        var newer = store.BeginRequest(new[] { A });
        Assert.Equal(AuthorizationChange.Unchanged, store.Apply(A, newer, null, Now));
        Assert.Equal(AuthorizationChange.Stale, store.Apply(A, older, Grant(A, StaffRole.Owner, Now), Now));
        Assert.Null(store.Get(A, Now));
    }

    [Fact]
    public void A_player_who_left_is_not_granted_by_a_lookup_still_in_flight()
    {
        var store = new AuthorizationStore(MaxAge);
        var ticket = store.BeginRequest(new[] { A });
        store.Forget(A);
        Assert.Equal(AuthorizationChange.Stale, store.Apply(A, ticket, Grant(A, StaffRole.Owner, Now), Now));
        Assert.Null(store.Get(A, Now));

        // Rejoining starts a fresh lookup that does apply.
        Assert.Equal(AuthorizationChange.Granted, store.Apply(A, store.BeginRequest(new[] { A }), Grant(A, StaffRole.Owner, Now), Now));
    }

    [Fact]
    public void Leaving_clears_the_grant()
    {
        var store = new AuthorizationStore(MaxAge);
        store.Apply(A, store.BeginRequest(new[] { A }), Grant(A, StaffRole.Admin, Now), Now);
        Assert.True(store.Forget(A));
        Assert.Null(store.Get(A, Now));
        Assert.False(store.Forget(A));
    }

    [Fact]
    public void Snapshot_lists_valid_grants_highest_role_first_and_clear_returns_everyone()
    {
        var store = new AuthorizationStore(MaxAge);
        store.Apply(A, store.BeginRequest(new[] { A }), Grant(A, StaffRole.Staff, Now), Now);
        store.Apply(B, store.BeginRequest(new[] { B }), Grant(B, StaffRole.Owner, Now), Now);
        store.Apply(C, store.BeginRequest(new[] { C }), Grant(C, StaffRole.Admin, Now, Now.AddSeconds(1)), Now);
        Assert.Equal(new[] { B, A }, store.Snapshot(Now.AddSeconds(2)).Select(g => g.SteamId).ToArray());
        Assert.Equal(3, store.Clear().Count);
        Assert.Empty(store.Snapshot(Now));
    }

    [Fact]
    public void Rejects_a_non_positive_max_age()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuthorizationStore(TimeSpan.Zero));
    }

    [Fact]
    public async Task Concurrent_lookups_leave_the_result_of_the_newest_one()
    {
        var store = new AuthorizationStore(MaxAge);
        var ids = Enumerable.Range(0, 32).Select(i => 76561198000002000UL + (ulong)i).ToArray();

        // 200 overlapping lookups; each answers with its own role. Whatever the completion order,
        // every player must end with the answer of the last lookup started for them.
        var lookups = Enumerable.Range(0, 200).Select(i => (Ticket: store.BeginRequest(ids), Role: (StaffRole)(1 + i % 4))).ToArray();
        var last = lookups[^1];
        await Task.WhenAll(lookups.Reverse().Select(lookup => Task.Run(() =>
        {
            foreach (var id in ids) store.Apply(id, lookup.Ticket, Grant(id, lookup.Role, Now), Now);
        })));

        Assert.All(ids, id => Assert.Equal(last.Role, store.Get(id, Now)!.Role));

        // Reads, expiry sweeps and leaves racing with answers never throw.
        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 2000; i++)
            {
                var id = ids[(worker + i) % ids.Length];
                var ticket = store.BeginRequest(new[] { id });
                if (i % 3 == 0) store.Forget(id);
                store.Apply(id, ticket, Grant(id, StaffRole.Admin, Now), Now);
                store.Get(id, Now);
                store.Snapshot(Now);
                store.RemoveExpired(Now.AddSeconds(i % 400));
            }
        })));
    }
}
