using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

internal sealed class WeaponSynchronization
{
    private readonly LegacyXSkinApiClient _api;
    private readonly WeaponPaintsConfig _config;
    private int _pollInFlight;

    internal WeaponSynchronization(LegacyXSkinApiClient api, WeaponPaintsConfig config)
    {
        _api = api;
        _config = config;
    }

    internal async Task OpenSessionAndApplyAsync(PlayerInfo player)
    {
        await _api.SendSessionAsync("session_connected", player);
        await PollAndApplyAsync();
    }

    internal Task GetPlayerData(PlayerInfo? player) => player is null ? Task.CompletedTask : OpenSessionAndApplyAsync(player);
    internal Task CloseSessionAsync(PlayerInfo player) => _api.SendSessionAsync("session_disconnected", player);

    internal async Task PollAndApplyAsync()
    {
        if (Interlocked.Exchange(ref _pollInFlight, 1) == 1) return;
        try
        {
            foreach (var job in await _api.ClaimJobsAsync())
            {
                var player = Utilities.GetPlayers().FirstOrDefault(candidate => candidate.IsValid && !candidate.IsBot && candidate.SteamID.ToString() == job.SteamId);
                if (player is null)
                {
                    await _api.AcknowledgeAsync(job, "failed", "player_not_connected", "Player is no longer connected to this server.");
                    continue;
                }
                Server.NextFrame(() =>
                {
                    try
                    {
                        ApplyLoadout(player, job.Payload);
                        _ = _api.AcknowledgeAsync(job, "applied", null, null);
                    }
                    catch (Exception exception)
                    {
                        _ = _api.AcknowledgeAsync(job, "failed", "apply_error", exception.Message);
                    }
                });
            }
        }
        catch (Exception exception)
        {
            Utility.Log($"LEGACY-X SkinBridge polling failed: {exception.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _pollInFlight, 0);
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
