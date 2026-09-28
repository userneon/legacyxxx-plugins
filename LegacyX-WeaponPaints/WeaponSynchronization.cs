using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

internal sealed class WeaponSynchronization
{
    private readonly LegacyXSkinApiClient _api;
    private readonly WeaponPaintsConfig _config;

    internal WeaponSynchronization(LegacyXSkinApiClient api, WeaponPaintsConfig config)
    {
        _api = api;
        _config = config;
    }

    /// <summary>
    /// Reads the player's saved loadout and applies it on the next game frame. A player without a
    /// LEGACY-X account gets an empty loadout (default items).
    /// </summary>
    internal async Task GetPlayerData(PlayerInfo? player)
    {
        if (player?.SteamId is not { Length: > 0 } steamId) return;
        try
        {
            var payload = await _api.GetLoadoutAsync(steamId) ?? new SkinchangerPayload();
            var applied = new TaskCompletionSource();
            Server.NextFrame(() =>
            {
                try
                {
                    var controller = Utilities.GetPlayers().FirstOrDefault(candidate => candidate.IsValid && !candidate.IsBot && candidate.SteamID.ToString() == steamId);
                    if (controller is not null) ApplyLoadout(controller, payload);
                }
                catch (Exception exception)
                {
                    Utility.Log($"LEGACY-X SkinBridge apply failed: {exception.Message}");
                }
                finally
                {
                    applied.TrySetResult();
                }
            });
            await applied.Task;
        }
        catch (Exception exception)
        {
            Utility.Log($"LEGACY-X SkinBridge loadout read failed: {exception.Message}");
        }
    }

    private void ApplyLoadout(CCSPlayerController player, SkinchangerPayload payload)
    {
        var slot = player.Slot;
        WeaponPaints.GPlayerWeaponsInfo.TryRemove(slot, out _);
        WeaponPaints.GPlayersKnife.TryRemove(slot, out _);
        WeaponPaints.GPlayersGlove.TryRemove(slot, out _);
        WeaponPaints.GPlayersAgent.TryRemove(slot, out _);
        WeaponPaints.GPlayersMusic.TryRemove(slot, out _);
        WeaponPaints.GPlayersPin.TryRemove(slot, out _);
        foreach (var entry in payload.Entries)
        {
            var teams = ToTeams(entry.TeamScope);
            // The site's catalog leaves weapon_defindex empty for skins, knives and gloves; the slot key names the item.
            var defindex = entry.WeaponDefindex ?? DefindexFromSlotKey(entry.SlotKey);
            switch (entry.Category)
            {
                case "weapon":
                case "weapon_skin":
                    if (!_config.Additional.SkinEnabled || defindex is null) continue;
                    SetWeaponInfo(slot, teams, defindex.Value, entry);
                    break;
                case "knife":
                    if (!_config.Additional.KnifeEnabled) continue;
                    var knifeName = !string.IsNullOrWhiteSpace(entry.Model) ? entry.Model
                        : defindex is { } knifeDefindex && WeaponPaints.WeaponDefindex.TryGetValue(knifeDefindex, out var designer) ? designer : null;
                    if (knifeName is null) continue;
                    var knives = WeaponPaints.GPlayersKnife.GetOrAdd(slot, _ => new ConcurrentDictionary<CsTeam, string>());
                    foreach (var team in teams) knives[team] = knifeName;
                    // The knife's finish is applied like any weapon skin, keyed by the knife's defindex.
                    if (defindex is not null && entry.PaintId is > 0) SetWeaponInfo(slot, teams, defindex.Value, entry);
                    break;
                case "glove":
                    if (!_config.Additional.GloveEnabled || defindex is null) continue;
                    var gloves = WeaponPaints.GPlayersGlove.GetOrAdd(slot, _ => new ConcurrentDictionary<CsTeam, ushort>());
                    foreach (var team in teams) gloves[team] = (ushort)defindex.Value;
                    // Gloves read their paint, seed and wear from the weapon info under the glove defindex.
                    SetWeaponInfo(slot, teams, defindex.Value, entry);
                    break;
                case "agent":
                    if (!_config.Additional.AgentEnabled || string.IsNullOrWhiteSpace(entry.Model)) continue;
                    var agents = WeaponPaints.GPlayersAgent.TryGetValue(slot, out var agent) ? agent : (CT: (string?)null, T: (string?)null);
                    foreach (var team in teams)
                    {
                        if (team == CsTeam.CounterTerrorist) agents.CT = entry.Model;
                        if (team == CsTeam.Terrorist) agents.T = entry.Model;
                    }
                    WeaponPaints.GPlayersAgent[slot] = agents;
                    break;
                case "music_kit":
                    if (!_config.Additional.MusicEnabled || entry.WeaponDefindex is null) continue;
                    var music = WeaponPaints.GPlayersMusic.GetOrAdd(slot, _ => new ConcurrentDictionary<CsTeam, ushort>());
                    foreach (var team in teams) music[team] = (ushort)entry.WeaponDefindex.Value;
                    break;
                case "pin":
                    if (!_config.Additional.PinsEnabled || entry.WeaponDefindex is null) continue;
                    var pins = WeaponPaints.GPlayersPin.GetOrAdd(slot, _ => new ConcurrentDictionary<CsTeam, ushort>());
                    foreach (var team in teams) pins[team] = (ushort)entry.WeaponDefindex.Value;
                    break;
            }
        }
    }

    private static void SetWeaponInfo(int slot, CsTeam[] teams, int defindex, SkinchangerEntry entry)
    {
        var weapons = WeaponPaints.GPlayerWeaponsInfo.GetOrAdd(slot, _ => new ConcurrentDictionary<CsTeam, ConcurrentDictionary<int, WeaponInfo>>());
        foreach (var team in teams)
        {
            var teamWeapons = weapons.GetOrAdd(team, _ => new ConcurrentDictionary<int, WeaponInfo>());
            teamWeapons[defindex] = BuildWeaponInfo(entry);
        }
    }

    private static WeaponInfo BuildWeaponInfo(SkinchangerEntry entry) => new WeaponInfo
    {
        Paint = entry.PaintId ?? 0,
        Seed = entry.Options.Seed ?? 0,
        Wear = entry.Options.Wear ?? 0.0001f,
        StatTrak = entry.Options.StatTrak ?? false,
        Nametag = entry.Options.NameTag ?? string.Empty,
        Stickers = entry.Options.Stickers
            .Where(sticker => sticker.Id is > 0 && sticker.Slot is >= 0 and <= 4)
            .OrderBy(sticker => sticker.Slot)
            .Select(sticker => new StickerInfo
            {
                Slot = sticker.Slot,
                Id = (uint)sticker.Id!.Value,
                Schema = (uint)(sticker.Schema ?? 1),
                OffsetX = sticker.OffsetX ?? 0,
                OffsetY = sticker.OffsetY ?? 0,
                Wear = sticker.Wear ?? 0,
                Scale = sticker.Scale ?? 1,
                Rotation = sticker.Rotation ?? 0,
            }).ToList(),
        KeyChain = entry.Options.Charm?.Id is > 0 ? new KeyChainInfo
        {
            Id = (uint)entry.Options.Charm.Id.Value,
            OffsetX = entry.Options.Charm.OffsetX ?? 0,
            OffsetY = entry.Options.Charm.OffsetY ?? 0,
            OffsetZ = entry.Options.Charm.OffsetZ ?? 0,
            Seed = (uint)(entry.Options.Charm.Seed ?? 0),
        } : null,
    };

    // CS2 glove item definitions; the site's glove slot keys are the glove type's slug.
    private static readonly Dictionary<string, int> GloveDefindex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["broken-fang-gloves"] = 4725, ["bloodhound-gloves"] = 5027, ["sport-gloves"] = 5030, ["driver-gloves"] = 5031,
        ["hand-wraps"] = 5032, ["moto-gloves"] = 5033, ["specialist-gloves"] = 5034, ["hydra-gloves"] = 5035,
    };

    /// <summary>weapon:9 → 9, knife:500 → 500, glove:sport-gloves → 5030; null when the key names nothing known.</summary>
    internal static int? DefindexFromSlotKey(string? slotKey)
    {
        var separator = slotKey?.IndexOf(':') ?? -1;
        if (slotKey is null || separator < 0) return null;
        var key = slotKey[(separator + 1)..].Trim();
        if (int.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && number > 0) return number;
        return GloveDefindex.TryGetValue(key, out var glove) ? glove : null;
    }

    private static CsTeam[] ToTeams(string? teamScope) => teamScope switch
    {
        "t" => [CsTeam.Terrorist],
        "ct" => [CsTeam.CounterTerrorist],
        _ => [CsTeam.Terrorist, CsTeam.CounterTerrorist],
    };

    // Website-controlled production flow intentionally disables in-game mutation commands.
    internal Task SyncKnifeToDatabase(PlayerInfo player, string knife, CsTeam[] teams) => Task.CompletedTask;
    internal Task SyncGloveToDatabase(PlayerInfo player, ushort gloveDefIndex, CsTeam[] teams) => Task.CompletedTask;
    internal Task SyncAgentToDatabase(PlayerInfo player) => Task.CompletedTask;
    internal Task SyncWeaponPaintsToDatabase(PlayerInfo player) => Task.CompletedTask;
    internal Task SyncMusicToDatabase(PlayerInfo player, ushort music, CsTeam[] teams) => Task.CompletedTask;
    internal Task SyncPinToDatabase(PlayerInfo player, ushort pin, CsTeam[] teams) => Task.CompletedTask;
    internal Task SyncStatTrakToDatabase(PlayerInfo player) => Task.CompletedTask;
}
