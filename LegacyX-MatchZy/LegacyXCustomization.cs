using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Timers;

namespace MatchZy;

public partial class MatchZy
{
    private readonly Random legacyXRandom = new();

    private string PickLegacyXNextMap()
    {
        var configuredMaps = legacyXMapPool.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(map => Server.IsMapValid(map))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (configuredMaps.Count == 0)
        {
            Log("[LEGACY-X] legacyx_map_pool has no valid installed maps; keeping the current map.");
            return Server.MapName;
        }

        string currentMap = Server.MapName;
        var candidates = configuredMaps
            .Where(map => !string.Equals(map, currentMap, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            return configuredMaps[0];
        }

        return candidates[legacyXRandom.Next(candidates.Count)];
    }

    private void BeginLegacyXPostMatchTransition(int restartDelay)
    {
        string nextMap = PickLegacyXNextMap();
        float delay = Math.Max(0.5f, legacyXMapTransitionDelay.Value);

        PrintToAllChat($"Match saved. Next map: {{white}}{nextMap}{{grey}}.");
        if (legacyXRestartAfterMatch.Value) PrintToAllChat("The server restarts for the next map: reconnect in about a minute.");
        Log($"[LEGACY-X] Post-match soft transition scheduled: {Server.MapName} -> {nextMap} in {delay:0.0}s");

        AddTimer(Math.Max(0, restartDelay), () =>
        {
            // Reset MatchZy lifecycle before changing the map so ready flags, scores,
            // demos, pause state and series state cannot leak into the next session.
            ResetMatch(false);

            AddTimer(delay, () =>
            {
                if (legacyXRestartAfterMatch.Value && TryWriteLegacyXNextMap(nextMap))
                {
                    // A whole restart, not a map change: the HUD layout and the plugins are loaded again. The service starts the server again
                    // (Restart=always) and OpenLegacyXNextMap changes to the saved map once it is up.
                    Log($"[LEGACY-X] Restarting the server for the next map {nextMap}.");
                    // Everyone is sent out with a clear reason first, so the players read "restarting" and not a lost connection.
                    foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV && p.UserId.HasValue))
                        Server.ExecuteCommand($"kickid {(ushort)player.UserId!.Value} \"Match finished. The server restarts, reconnect in a minute.\"");
                    AddTimer(1.5f, () => Server.ExecuteCommand("quit"));
                    return;
                }
                ChangeMap(nextMap, 0.0f);
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    private static string LegacyXNextMapFile => Path.Combine(Server.GameDirectory, "csgo", "legacyx_next_map.txt");

    private bool TryWriteLegacyXNextMap(string map)
    {
        try { File.WriteAllText(LegacyXNextMapFile, map); return true; }
        catch (Exception ex) { Log($"[LEGACY-X] Could not save the next map, changing the map without a restart: {ex.Message}"); return false; }
    }

    /// <summary>After a restart for the next map: change to the map saved before the restart (once; the file is removed first, so a failure cannot loop).</summary>
    private void RegisterLegacyXNextMap()
    {
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            try
            {
                var file = LegacyXNextMapFile;
                if (!File.Exists(file)) return;
                var map = File.ReadAllText(file).Trim();
                File.Delete(file);
                if (map.Length == 0 || string.Equals(map, Server.MapName, StringComparison.OrdinalIgnoreCase)) return;
                AddTimer(5f, () =>
                {
                    if (Server.IsMapValid(map)) ChangeMap(map, 0.0f);
                    else Log($"[LEGACY-X] The saved next map {map} is not valid here; staying on {Server.MapName}.");
                });
            }
            catch (Exception ex) { Log($"[LEGACY-X] Could not open the saved next map: {ex.Message}"); }
        });
    }
}
