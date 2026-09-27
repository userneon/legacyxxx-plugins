using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace LegacyX.Admin.Authorization;

/// <summary>A player's confirmed in-game role on this server, as of <see cref="CheckedAt"/>.</summary>
public sealed record PlayerAuthorization(ulong SteamId, StaffRole Role, DateTimeOffset? ExpiresAt, DateTimeOffset CheckedAt)
{
    public bool IsStaff => Role != StaffRole.Player;

    /// <summary>Still usable: a staff role, not past its expiry, and confirmed within <paramref name="maxAge"/>.</summary>
    public bool IsValidAt(DateTimeOffset now, TimeSpan maxAge) =>
        IsStaff && (ExpiresAt is null || ExpiresAt > now) && now - CheckedAt < maxAge;
}

/// <summary>
/// Parses POST /api/v1/plugin/admin/authorizations. The response as a whole is rejected (null) when
/// it is not the expected shape or is for another server; a single malformed or non-active entry
/// only makes that player a <see cref="StaffRole.Player"/>. Requested SteamIDs missing from the
/// response are players too.
/// </summary>
public static class AuthorizationResponseParser
{
    public static IReadOnlyDictionary<ulong, PlayerAuthorization>? Parse(
        string json, string expectedServerId, IReadOnlyCollection<ulong> requested, DateTimeOffset now, out string? error)
    {
        error = null;
        var result = new Dictionary<ulong, PlayerAuthorization>();
        foreach (var steamId in requested) result[steamId] = new PlayerAuthorization(steamId, StaffRole.Player, null, now);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            error = "response is not JSON";
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "response is not an object";
                return null;
            }
            if (!root.TryGetProperty("serverId", out var serverId) || serverId.ValueKind != JsonValueKind.String || serverId.GetString() != expectedServerId)
            {
                error = "response is for another server";
                return null;
            }
            if (!root.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array)
            {
                error = "response has no players array";
                return null;
            }

            var seen = new HashSet<ulong>();
            foreach (var entry in players.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) continue;
                if (!entry.TryGetProperty("steamId", out var steamIdValue) || steamIdValue.ValueKind != JsonValueKind.String) continue;
                if (!TryParseSteamId64(steamIdValue.GetString(), out var steamId) || !result.ContainsKey(steamId)) continue;
                if (!seen.Add(steamId))
                {
                    error = "response lists a player twice";
                    return null;
                }
                result[steamId] = ParseEntry(entry, steamId, now);
            }
        }
        return result;
    }

    private static PlayerAuthorization ParseEntry(JsonElement entry, ulong steamId, DateTimeOffset now)
    {
        var player = new PlayerAuthorization(steamId, StaffRole.Player, null, now);
        if (!entry.TryGetProperty("authorized", out var authorized) || authorized.ValueKind != JsonValueKind.True) return player;
        if (!entry.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String || status.GetString() != "active") return player;
        if (!entry.TryGetProperty("role", out var roleValue) || roleValue.ValueKind != JsonValueKind.String) return player;
        if (!StaffPermissions.TryParseRole(roleValue.GetString(), out var role) || role == StaffRole.Player) return player;

        DateTimeOffset? expiresAt = null;
        if (entry.TryGetProperty("expiresAt", out var expiresValue) && expiresValue.ValueKind != JsonValueKind.Null)
        {
            if (expiresValue.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(expiresValue.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                return player;
            if (parsed <= now) return player;
            expiresAt = parsed;
        }
        return new PlayerAuthorization(steamId, role, expiresAt, now);
    }

    public static bool TryParseSteamId64(string? value, out ulong steamId)
    {
        steamId = 0;
        return value is { Length: 17 } && value.StartsWith("7656119", StringComparison.Ordinal) &&
               ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out steamId);
    }
}
