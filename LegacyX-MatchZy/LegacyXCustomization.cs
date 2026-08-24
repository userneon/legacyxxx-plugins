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

        Server.PrintToChatAll($"{chatPrefix} PLEASE WAIT — match saved. Next map: {nextMap}");
        Log($"[LEGACY-X] Post-match soft transition scheduled: {Server.MapName} -> {nextMap} in {delay:0.0}s");

        AddTimer(Math.Max(0, restartDelay), () =>
        {
            // Reset MatchZy lifecycle before changing the map so ready flags, scores,
            // demos, pause state and series state cannot leak into the next session.
            ResetMatch(false);

            AddTimer(delay, () =>
            {
                ChangeMap(nextMap, 0.0f);
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }
}
