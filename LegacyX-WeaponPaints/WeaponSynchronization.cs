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
        if (string.IsNullOrEmpty(player?.SteamId)) return;
        var steamId = player.SteamId;
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
            switch (entry.Category)
            {
                case "weapon":
                case "weapon_skin":
                    if (!_config.Additional.SkinEnabled || entry.WeaponDefindex is null) continue;
                    var weapons = WeaponPaints.GPlayerWeaponsInfo.GetOrAdd(slot, _ => new ConcurrentDictionary<CsTeam, ConcurrentDictionary<int, WeaponInfo>>());
                    foreach (var team in teams)
                    {
                        var teamWeapons = weapons.GetOrAdd(team, _ => new ConcurrentDictionary<int, WeaponInfo>());
						teamWeapons[entry.WeaponDefindex.Value] = new WeaponInfo
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
                    }
                    break;
                case "knife":
                    if (!_config.Additional.KnifeEnabled || string.IsNullOrWhiteSpace(entry.Model)) continue;
                    var knives = WeaponPaints.GPlayersKnife.GetOrAdd(slot, _ => new ConcurrentDictionary<CsTeam, string>());
                    foreach (var team in teams) knives[team] = entry.Model;
                    break;
                case "glove":
                    if (!_config.Additional.GloveEnabled || entry.WeaponDefindex is null) continue;
                    var gloves = WeaponPaints.GPlayersGlove.GetOrAdd(slot, _ => new ConcurrentDictionary<CsTeam, ushort>());
                    foreach (var team in teams) gloves[team] = (ushort)entry.WeaponDefindex.Value;
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
