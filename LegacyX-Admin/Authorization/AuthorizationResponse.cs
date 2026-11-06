using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace LegacyX.Admin.Authorization;

/// <summary>A player's confirmed in-game role on this server, as of <see cref="CheckedAt"/>.</summary>
/// <summary>
/// What the API says a role may do, as the Owner set it on the staff site (Staff → Roles): the CounterStrikeSharp flags, the commands (without
/// "css_") and the immunity. When it is present it replaces the built-in grant of the role; the Owner role never carries one.
/// </summary>
/// <summary>A limit the Owner set on one command for a role (Staff → Roles): seconds to wait between two uses (0 = none) and uses per map (0 = no cap).</summary>
public sealed record CommandLimit(int CooldownSeconds, int PerMap);

public sealed record CustomGrant(IReadOnlyList<string> Flags, IReadOnlyList<string> Commands, uint Immunity, IReadOnlyDictionary<string, CommandLimit>? Limits = null)
{
    public bool SameAs(CustomGrant? other) =>
        other != null && Immunity == other.Immunity && Flags.OrderBy(f => f).SequenceEqual(other.Flags.OrderBy(f => f)) && Commands.OrderBy(c => c).SequenceEqual(other.Commands.OrderBy(c => c))
        && (Limits ?? EmptyLimits).OrderBy(l => l.Key, StringComparer.Ordinal).SequenceEqual((other.Limits ?? EmptyLimits).OrderBy(l => l.Key, StringComparer.Ordinal));

    private static readonly IReadOnlyDictionary<string, CommandLimit> EmptyLimits = new Dictionary<string, CommandLimit>();
}

public sealed record PlayerAuthorization(ulong SteamId, StaffRole Role, DateTimeOffset? ExpiresAt, DateTimeOffset CheckedAt, string ClanTag = "", CustomGrant? Grant = null)
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
        return new PlayerAuthorization(steamId, role, expiresAt, now, ParseClanTag(entry), role == StaffRole.Owner ? null : ParseGrant(entry));
    }

    /// <summary>The edited role, if the API sent a well-formed one: flags like "@css/slay", command names, immunity 0 to 900. Anything else means no custom grant (the built-in role applies).</summary>
    private static CustomGrant? ParseGrant(JsonElement entry)
    {
        if (!entry.TryGetProperty("grant", out var grant) || grant.ValueKind != JsonValueKind.Object) return null;
        if (!grant.TryGetProperty("flags", out var flagsValue) || flagsValue.ValueKind != JsonValueKind.Array) return null;
        if (!grant.TryGetProperty("commands", out var commandsValue) || commandsValue.ValueKind != JsonValueKind.Array) return null;
        if (!grant.TryGetProperty("immunity", out var immunityValue) || immunityValue.ValueKind != JsonValueKind.Number || !immunityValue.TryGetInt32(out var immunity) || immunity < 0 || immunity > 900) return null;
        var flags = new List<string>();
        foreach (var flag in flagsValue.EnumerateArray())
        {
            var text = flag.ValueKind == JsonValueKind.String ? flag.GetString() : null;
            if (text == null || text.Length > 40 || !System.Text.RegularExpressions.Regex.IsMatch(text, "^@[a-z0-9_]+/[a-z0-9_]+$")) return null;
            flags.Add(text);
        }
        var commands = new List<string>();
        foreach (var command in commandsValue.EnumerateArray())
        {
            var text = command.ValueKind == JsonValueKind.String ? command.GetString() : null;
            if (text == null || text.Length > 40 || !System.Text.RegularExpressions.Regex.IsMatch(text, "^[a-z0-9_]+$")) return null;
            commands.Add(text);
        }
        if (flags.Count > 40 || commands.Count > 200) return null;
        return new CustomGrant(flags, commands, (uint)immunity, ParseLimits(grant));
    }

    /// <summary>The per-command limits, if the API sent any: { "blind": { "cooldown": 30, "perMap": 5 } }. An entry that is not well-formed is left out; none means no limits.</summary>
    private static IReadOnlyDictionary<string, CommandLimit>? ParseLimits(JsonElement grant)
    {
        if (!grant.TryGetProperty("limits", out var limits) || limits.ValueKind != JsonValueKind.Object) return null;
        var result = new Dictionary<string, CommandLimit>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in limits.EnumerateObject())
        {
            if (result.Count >= 200 || property.Name.Length > 40 || !System.Text.RegularExpressions.Regex.IsMatch(property.Name, "^[a-z0-9_]+$")) continue;
            if (property.Value.ValueKind != JsonValueKind.Object) continue;
            var cooldown = property.Value.TryGetProperty("cooldown", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var cs) ? cs : 0;
            var perMap = property.Value.TryGetProperty("perMap", out var m) && m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out var pm) ? pm : 0;
            if (cooldown < 0 || cooldown > 3600 || perMap < 0 || perMap > 999 || (cooldown == 0 && perMap == 0)) continue;
            result[property.Name] = new CommandLimit(cooldown, perMap);
        }
        return result.Count == 0 ? null : result;
    }

    /// <summary>The website clan tag, if the API sent one: upper-case letters, digits and - _ . only, at most 8 (it is shown in the Tab tag).</summary>
    private static string ParseClanTag(JsonElement entry)
    {
        if (!entry.TryGetProperty("clanTag", out var value) || value.ValueKind != JsonValueKind.String) return "";
        var clean = new string((value.GetString() ?? "").Trim().ToUpperInvariant().Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray());
        return clean.Length > 8 ? clean[..8] : clean;
    }

    public static bool TryParseSteamId64(string? value, out ulong steamId)
    {
        steamId = 0;
        return value is { Length: 17 } && value.StartsWith("7656119", StringComparison.Ordinal) &&
               ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out steamId);
    }
}
