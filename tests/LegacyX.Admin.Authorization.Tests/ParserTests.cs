using System;
using System.Linq;
using LegacyX.Admin.Authorization;
using Xunit;

namespace LegacyX.Admin.Authorization.Tests;

public class ParserTests
{
    internal const ulong A = 76561198000000001, B = 76561198000000002, C = 76561198000000003;
    internal static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    internal static string Entry(ulong steamId, bool authorized, string role, string status = "active", string? expiresAt = null) =>
        $"{{\"steamId\":\"{steamId}\",\"authorized\":{(authorized ? "true" : "false")},\"role\":\"{role}\",\"status\":\"{status}\",\"source\":\"global\",\"expiresAt\":{(expiresAt == null ? "null" : $"\"{expiresAt}\"")}}}";

    internal static string Body(string serverId, params string[] entries) =>
        $"{{\"serverId\":\"{serverId}\",\"checkedAt\":\"2026-09-27T12:00:00.000Z\",\"players\":[{string.Join(",", entries)}]}}";

    private static StaffRole? RoleOf(string json, ulong steamId, params ulong[] requested)
    {
        var result = AuthorizationResponseParser.Parse(json, "srv-1", requested.Length > 0 ? requested : new[] { steamId }, Now, out _);
        return result?[steamId].Role;
    }

    [Theory]
    [InlineData("owner", StaffRole.Owner)]
    [InlineData("manager", StaffRole.Manager)]
    [InlineData("admin", StaffRole.Admin)]
    [InlineData("staff", StaffRole.Staff)]
    public void Authorizes_active_roles(string role, StaffRole expected)
    {
        Assert.Equal(expected, RoleOf(Body("srv-1", Entry(A, true, role)), A));
    }

    [Theory]
    [InlineData(false, "admin", "active")]      // not authorized
    [InlineData(true, "player", "active")]      // player role
    [InlineData(true, "admin", "suspended")]    // inactive
    [InlineData(true, "admin", "revoked")]
    [InlineData(true, "admin", "expired")]
    [InlineData(true, "admin", "none")]
    [InlineData(true, "root", "active")]        // unknown role
    [InlineData(true, "Admin", "active")]
    public void Denies_everything_else(bool authorized, string role, string status)
    {
        Assert.Equal(StaffRole.Player, RoleOf(Body("srv-1", Entry(A, authorized, role, status)), A));
    }

    [Fact]
    public void Keeps_a_future_expiry_and_denies_a_past_or_unreadable_one()
    {
        var future = AuthorizationResponseParser.Parse(Body("srv-1", Entry(A, true, "staff", expiresAt: "2026-09-28T00:00:00.000Z")), "srv-1", new[] { A }, Now, out _)!;
        Assert.Equal(StaffRole.Staff, future[A].Role);
        Assert.Equal(new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero), future[A].ExpiresAt);

        Assert.Equal(StaffRole.Player, RoleOf(Body("srv-1", Entry(A, true, "staff", expiresAt: "2026-09-27T11:59:59.000Z")), A));
        Assert.Equal(StaffRole.Player, RoleOf(Body("srv-1", Entry(A, true, "staff", expiresAt: "not a date")), A));
    }

    [Fact]
    public void Requested_players_missing_from_the_answer_are_players()
    {
        var result = AuthorizationResponseParser.Parse(Body("srv-1", Entry(A, true, "owner")), "srv-1", new[] { A, B }, Now, out _)!;
        Assert.Equal(StaffRole.Owner, result[A].Role);
        Assert.Equal(StaffRole.Player, result[B].Role);
    }

    [Fact]
    public void Ignores_players_that_were_not_asked_about_and_malformed_ids()
    {
        var json = Body("srv-1", Entry(C, true, "owner"), "{\"steamId\":\"123\",\"authorized\":true,\"role\":\"owner\",\"status\":\"active\"}", Entry(A, true, "admin"));
        var result = AuthorizationResponseParser.Parse(json, "srv-1", new[] { A }, Now, out _)!;
        Assert.Equal(new[] { A }, result.Keys.ToArray());
        Assert.Equal(StaffRole.Admin, result[A].Role);
    }

    [Fact]
    public void Treats_non_boolean_authorized_as_denied()
    {
        var json = Body("srv-1", $"{{\"steamId\":\"{A}\",\"authorized\":\"true\",\"role\":\"owner\",\"status\":\"active\"}}");
        Assert.Equal(StaffRole.Player, RoleOf(json, A));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"players\":[]}")]
    [InlineData("{\"serverId\":\"srv-1\"}")]
    [InlineData("{\"serverId\":\"srv-1\",\"players\":{}}")]
    [InlineData("{\"serverId\":\"srv-2\",\"players\":[]}")]
    public void Rejects_responses_it_cannot_trust(string json)
    {
        Assert.Null(AuthorizationResponseParser.Parse(json, "srv-1", new[] { A }, Now, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Rejects_a_response_that_lists_a_player_twice()
    {
        var json = Body("srv-1", Entry(A, false, "player", "none"), Entry(A, true, "owner"));
        Assert.Null(AuthorizationResponseParser.Parse(json, "srv-1", new[] { A }, Now, out _));
    }

    [Fact]
    public void Reads_the_exact_backend_response_shape()
    {
        // Captured from resolveAuthorizations() in the backend (server/legacyX/adminAuthorization.ts).
        const string json = """
        {"serverId":"eu-5v5-1","checkedAt":"2026-09-27T12:00:00.000Z","players":[
          {"steamId":"76561198000000001","authorized":true,"role":"owner","status":"active","source":"global","expiresAt":null},
          {"steamId":"76561198000000002","authorized":true,"role":"staff","status":"active","source":"server","expiresAt":"2026-09-28T00:00:00.000Z"},
          {"steamId":"76561198000000003","authorized":false,"role":"player","status":"revoked","source":"server","expiresAt":null}
        ]}
        """;
        var result = AuthorizationResponseParser.Parse(json, "eu-5v5-1", new[] { A, B, C }, Now, out _)!;
        Assert.Equal(StaffRole.Owner, result[A].Role);
        Assert.Equal(StaffRole.Staff, result[B].Role);
        Assert.Equal(StaffRole.Player, result[C].Role);
    }

    [Theory]
    [InlineData("76561198000000001", true)]
    [InlineData("7656119800000000", false)]
    [InlineData("765611980000000011", false)]
    [InlineData("12345678901234567", false)]
    [InlineData("+7656119800000000", false)]
    [InlineData("", false)]
    public void Accepts_only_steamid64(string value, bool expected)
    {
        Assert.Equal(expected, AuthorizationResponseParser.TryParseSteamId64(value, out _));
    }
}
