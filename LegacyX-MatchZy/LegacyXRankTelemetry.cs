using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

/// <summary>
/// Raw per-player telemetry for the Legacy-X rank system (competitive_result schema v2,
/// docs/PLUGIN_RANKED_TELEMETRY_V2.md in the API repository). The plugin only reports what happened; the API
/// calculates every EXP change. Counters CS2 already tracks (kills, assists, multi-kill rounds, entry kills,
/// headshots) come from MatchStats; plants, defuses, clutches, rounds played and leavers are tracked here.
/// </summary>
public partial class MatchZy
{
    private sealed class RankCounters
    {
        public string Name { get; set; } = string.Empty;
        public int RoundsPlayed { get; set; }
        public int BombPlants { get; set; }
        public int BombDefuses { get; set; }
        public int ClutchesWon { get; set; }
        public int ClutchesWon1v3Plus { get; set; }
        public int? LeftAtRound { get; set; }
        /// <summary>Last MatchStats seen for the player, kept when they disconnect.</summary>
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int HeadshotKills { get; set; }
        public int EntryKills { get; set; }
        public int Rounds3k { get; set; }
        public int Rounds4k { get; set; }
        public int Rounds5k { get; set; }
        public int Mvps { get; set; }
    }

    private readonly Dictionary<string, RankCounters> rankCounters = new(StringComparer.Ordinal);
    /// <summary>Per side (2 = T, 3 = CT): the last player alive and how many enemies were alive then.</summary>
    private readonly Dictionary<int, (string SteamId, int Enemies)> rankClutchCandidates = new();
    /// <summary>Match Core team1 is the side that started CT; MatchZy's own team1 may have started T.</summary>
    private bool rankCoreTeam1IsMatchzyTeam1 = true;
    private string legacyXMatchMode = "5v5";

    private RankCounters CountersFor(string steamId, string name)
    {
        if (!rankCounters.TryGetValue(steamId, out var counters))
        {
            counters = new RankCounters();
            rankCounters[steamId] = counters;
        }
        if (!string.IsNullOrWhiteSpace(name)) counters.Name = name;
        return counters;
    }

    private bool IsRankTelemetryActive => IsMatchCoreEnabled && matchCoreId.HasValue && isMatchLive;

    private static bool IsRankHuman(CCSPlayerController? player) =>
        player != null && player.IsValid && !player.IsBot && !player.IsHLTV && player.TeamNum is 2 or 3;

    private void ResetLegacyXRankTelemetry()
    {
        rankCounters.Clear();
        rankClutchCandidates.Clear();
        rankCoreTeam1IsMatchzyTeam1 = teamSides.TryGetValue(matchzyTeam1, out var side) && side == "CT";
    }

    /// <summary>LEGACYX_SERVER_MODE (e.g. competitive_5v5, pro_league, fun_retake) → the result's mode.</summary>
    private void SetLegacyXMatchMode(string serverMode)
    {
        var mode = serverMode.Trim().ToLowerInvariant();
        legacyXMatchMode = mode.Contains("pro") ? "pro_league" : mode.StartsWith("fun", StringComparison.Ordinal) ? "fun" : "5v5";
    }

    private void RegisterLegacyXRankTelemetry()
    {
        RegisterEventHandler<EventBombPlanted>((@event, _) =>
        {
            if (IsRankTelemetryActive && IsRankHuman(@event.Userid)) CountersFor(CoreSteamId(@event.Userid!), @event.Userid!.PlayerName).BombPlants++;
            return HookResult.Continue;
        });
        RegisterEventHandler<EventBombDefused>((@event, _) =>
        {
            if (IsRankTelemetryActive && IsRankHuman(@event.Userid)) CountersFor(CoreSteamId(@event.Userid!), @event.Userid!.PlayerName).BombDefuses++;
            return HookResult.Continue;
        });
        RegisterEventHandler<EventRoundStart>((_, _) =>
        {
            rankClutchCandidates.Clear();
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDeath>((@event, _) =>
        {
            if (!IsRankTelemetryActive) return HookResult.Continue;
            // Next frame, so the victim no longer counts as alive.
            Server.NextFrame(TrackRankClutchCandidates);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventRoundEnd>((@event, _) =>
        {
            if (!IsRankTelemetryActive) return HookResult.Continue;
            foreach (var player in Utilities.GetPlayers().Where(IsRankHuman))
            {
                CountersFor(CoreSteamId(player), player.PlayerName).RoundsPlayed++;
            }
            if (rankClutchCandidates.TryGetValue(@event.Winner, out var clutch))
            {
                var counters = CountersFor(clutch.SteamId, string.Empty);
                counters.ClutchesWon++;
                if (clutch.Enemies >= 3) counters.ClutchesWon1v3Plus++;
            }
            rankClutchCandidates.Clear();
            return HookResult.Continue;
        });
    }

    private void TrackRankClutchCandidates()
    {
        var alive = Utilities.GetPlayers().Where(player => IsRankHuman(player) && player.PawnIsAlive).ToList();
        foreach (var side in new[] { 2, 3 })
        {
            if (rankClutchCandidates.ContainsKey(side)) continue;
            var own = alive.Where(player => player.TeamNum == side).ToList();
            var enemies = alive.Count(player => player.TeamNum != side);
            if (own.Count == 1 && enemies >= 1) rankClutchCandidates[side] = (CoreSteamId(own[0]), enemies);
        }
    }

    /// <summary>Keeps a leaving player's MatchStats and the round they left at.</summary>
    private void OnLegacyXRankDisconnect(CCSPlayerController player)
    {
        if (!IsRankTelemetryActive || player.IsBot) return;
        CaptureRankMatchStats(player);
        var rules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        CountersFor(CoreSteamId(player), player.PlayerName).LeftAtRound = rules?.TotalRoundsPlayed ?? 0;
    }

    private void OnLegacyXRankReconnect(CCSPlayerController player)
    {
        if (rankCounters.TryGetValue(CoreSteamId(player), out var counters)) counters.LeftAtRound = null;
    }

    private void CaptureRankMatchStats(CCSPlayerController player)
    {
        var stats = player.ActionTrackingServices?.MatchStats;
        if (stats == null) return;
        var counters = CountersFor(CoreSteamId(player), player.PlayerName);
        counters.Kills = stats.Kills;
        counters.Deaths = stats.Deaths;
        counters.Assists = stats.Assists;
        counters.HeadshotKills = stats.HeadShotKills;
        counters.EntryKills = stats.EntryWins;
        counters.Rounds3k = stats.Enemy3Ks;
        counters.Rounds4k = stats.Enemy4Ks;
        counters.Rounds5k = stats.Enemy5Ks;
        counters.Mvps = player.MVPs;
    }

    /// <summary>Converts a MatchZy team key ("team1" = matchzyTeam1) into the Match Core key.</summary>
    private string CoreTeamFromMatchzy(string matchzyTeam) => matchzyTeam switch
    {
        "team1" => rankCoreTeam1IsMatchzyTeam1 ? "team1" : "team2",
        "team2" => rankCoreTeam1IsMatchzyTeam1 ? "team2" : "team1",
        _ => matchzyTeam,
    };

    /// <summary>competitive_result v2 for result_final. mapTeam1Rounds/mapTeam2Rounds are MatchZy team1/team2 map rounds.</summary>
    private object BuildCompetitiveResult(int mapTeam1Rounds, int mapTeam2Rounds, bool finishedNormally)
    {
        var connected = Utilities.GetPlayers().Where(IsRankHuman).ToList();
        foreach (var player in connected) CaptureRankMatchStats(player);

        var coreTeam1Rounds = rankCoreTeam1IsMatchzyTeam1 ? mapTeam1Rounds : mapTeam2Rounds;
        var coreTeam2Rounds = rankCoreTeam1IsMatchzyTeam1 ? mapTeam2Rounds : mapTeam1Rounds;
        var totalRounds = mapTeam1Rounds + mapTeam2Rounds;
        var connectedIds = connected.Select(CoreSteamId).ToHashSet(StringComparer.Ordinal);

        var players = new List<object>();
        foreach (var (steamId, counters) in rankCounters)
        {
            // Team: the Match Core slot (original or fill); otherwise the side the player is on now.
            var slot = matchCoreSlotsByOriginal.Values.FirstOrDefault(candidate => candidate.OriginalSteamId == steamId || candidate.ActiveSteamId == steamId);
            string? team = slot?.TeamKey;
            if (team == null)
            {
                var player = connected.FirstOrDefault(candidate => CoreSteamId(candidate) == steamId);
                if (player != null) team = CoreTeamKey(player);
            }
            if (team == null || counters.RoundsPlayed == 0 && counters.Kills == 0 && counters.Deaths == 0) continue;

            var isOriginal = matchCoreSlotsByOriginal.ContainsKey(steamId);
            var leftEarly = !connectedIds.Contains(steamId) && counters.LeftAtRound != null;
            players.Add(new
            {
                steam_id = steamId,
                team,
                is_bot = false,
                fill = !isOriginal,
                rounds_played = Math.Min(counters.RoundsPlayed, totalRounds),
                kills = counters.Kills,
                deaths = counters.Deaths,
                assists = counters.Assists,
                headshot_kills = counters.HeadshotKills,
                entry_kills = counters.EntryKills,
                bomb_plants = counters.BombPlants,
                bomb_defuses = counters.BombDefuses,
                rounds_3k = counters.Rounds3k,
                rounds_4k = counters.Rounds4k,
                rounds_5k = counters.Rounds5k,
                clutches_won = counters.ClutchesWon,
                clutches_won_1v3_plus = counters.ClutchesWon1v3Plus,
                mvps = counters.Mvps,
                left_early = leftEarly,
                left_at_round = leftEarly ? counters.LeftAtRound : null,
            });
        }

        return new
        {
            schema_version = 2,
            mode = legacyXMatchMode,
            finished_normally = finishedNormally,
            human_players_at_end = connected.Count,
            total_rounds = totalRounds,
            team1 = new { rounds_won = coreTeam1Rounds },
            team2 = new { rounds_won = coreTeam2Rounds },
            players,
            unavailable_fields = Array.Empty<string>(),
        };
    }
}
