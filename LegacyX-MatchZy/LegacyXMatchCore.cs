using System.Net.Http;
using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

public partial class MatchZy
{
    private sealed class CoreSlot
    {
        public required string TeamKey { get; init; }
        public required int SlotIndex { get; init; }
        public required string OriginalSteamId { get; init; }
        public required string OriginalName { get; init; }
        public string? ActiveSteamId { get; set; }
        public bool IsFill { get; set; }
    }

    private sealed class CoreSnapshot
    {
        public int Health { get; init; }
        public int Armor { get; init; }
        public required string[] Weapons { get; init; }
        public float X { get; init; }
        public float Y { get; init; }
        public float Z { get; init; }
        public float Pitch { get; init; }
        public float Yaw { get; init; }
        public float Roll { get; init; }
    }

    private readonly object matchCoreQueueLock = new();
    private Task matchCoreQueue = Task.CompletedTask;
    private readonly HttpClient matchCoreHttp = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly Dictionary<string, CoreSlot> matchCoreSlotsByOriginal = new();
    private readonly Dictionary<string, CoreSnapshot> matchCoreSnapshots = new();
    private readonly Dictionary<string, CounterStrikeSharp.API.Modules.Timers.Timer> matchCoreFillTimers = new();
    private Guid? matchCoreId;
    private int matchCoreRevision;
    private int matchCoreEventSequence;
    private bool matchCoreLiveQueued;
    private bool matchCoreRemoteSlotsReady;

    private bool IsMatchCoreEnabled => legacyXMatchCoreEnabled.Value && !string.IsNullOrWhiteSpace(legacyXMatchCoreApiUrl.Value) && !string.IsNullOrWhiteSpace(legacyXMatchCorePluginSecret.Value);

    private static string CoreSteamId(CCSPlayerController player) => player.SteamID.ToString();

    private string? CoreTeamKey(CCSPlayerController player)
    {
        var configuredTeam = GetPlayerTeam(player);
        if (configuredTeam == CsTeam.CounterTerrorist) return "team1";
        if (configuredTeam == CsTeam.Terrorist) return "team2";
        return null;
    }

    private void BeginLegacyXMatchCore()
    {
        if (!IsMatchCoreEnabled) return;

        var roster = playerData.Values
            .Where(player => IsPlayerValid(player) && !player.IsBot && !player.IsHLTV)
            .Select(player => new { Player = player, TeamKey = CoreTeamKey(player) })
            .Where(entry => entry.TeamKey is not null)
            .GroupBy(entry => entry.TeamKey!)
            .ToDictionary(group => group.Key, group => group.OrderBy(entry => CoreSteamId(entry.Player)).ToList());

        if (!roster.TryGetValue("team1", out var team1) || !roster.TryGetValue("team2", out var team2) || team1.Count != 5 || team2.Count != 5)
        {
            Log("[LegacyXMatchCore] Match Core creation refused because the active roster is not exactly 5v5.");
            return;
        }

        matchCoreId = Guid.NewGuid();
        matchCoreRevision = 0;
        matchCoreEventSequence = 0;
        matchCoreLiveQueued = false;
        matchCoreRemoteSlotsReady = true;
        matchCoreSlotsByOriginal.Clear();
        matchCoreSnapshots.Clear();
        ResetLegacyXRankTelemetry();

        var participants = new List<object>();
        foreach (var group in roster)
        {
            for (var index = 0; index < group.Value.Count; index++)
            {
                var player = group.Value[index].Player;
                var steamId = CoreSteamId(player);
                var slot = new CoreSlot
                {
                    TeamKey = group.Key,
                    SlotIndex = index + 1,
                    OriginalSteamId = steamId,
                    OriginalName = player.PlayerName,
                    ActiveSteamId = steamId,
                    IsFill = false,
                };
                matchCoreSlotsByOriginal[steamId] = slot;
                participants.Add(new { steam_id = steamId, name = player.PlayerName, team_key = group.Key, slot_index = index + 1 });
            }
        }

        var coreId = matchCoreId.Value;
        QueueMatchCoreEvent("match_created", _ => new
        {
            event_id = NextMatchCoreEventId("match_created"),
            event_type = "match_created",
            match_id = coreId.ToString(),
            server_id = legacyXMatchCoreServerId.Value,
            matchzy_local_id = liveMatchId.ToString(),
            map_name = Server.MapName,
            map_number = matchConfig.CurrentMapNumber,
            participants,
        });
    }

    private void OnLegacyXMatchCoreLive()
    {
        if (!IsMatchCoreEnabled || !matchCoreId.HasValue || matchCoreLiveQueued) return;
        matchCoreLiveQueued = true;
        QueueMatchCoreEvent("state_transition_live", expectedRevision => new
        {
            event_id = NextMatchCoreEventId("live"),
            event_type = "state_transition",
            match_id = matchCoreId.Value.ToString(),
            expected_revision = expectedRevision,
            state = "LIVE",
        });
    }

    private void OnLegacyXMatchCoreDisconnect(CCSPlayerController player)
    {
        if (!IsMatchCoreEnabled || !matchCoreId.HasValue || !isMatchLive) return;
        OnLegacyXRankDisconnect(player);
        var steamId = CoreSteamId(player);
        var slot = matchCoreSlotsByOriginal.Values.FirstOrDefault(candidate => candidate.ActiveSteamId == steamId);
        if (slot == null) return;

        var snapshot = CaptureMatchCoreSnapshot(player);
        if (snapshot != null)
        {
            matchCoreSnapshots[steamId] = snapshot;
            QueueMatchCoreEvent("snapshot_saved", expectedRevision => new
            {
                event_id = NextMatchCoreEventId("snapshot"),
                event_type = "snapshot_saved",
                match_id = matchCoreId.Value.ToString(),
                expected_revision = expectedRevision,
                steam_id = steamId,
                snapshot = new
                {
                    health = snapshot.Health,
                    armor = snapshot.Armor,
                    weapons = snapshot.Weapons,
                    position = new { x = snapshot.X, y = snapshot.Y, z = snapshot.Z, pitch = snapshot.Pitch, yaw = snapshot.Yaw, roll = snapshot.Roll },
                },
            });
        }

        slot.ActiveSteamId = null;
        var wasTemporaryFill = slot.IsFill;
        slot.IsFill = false;
        matchCoreRemoteSlotsReady = false;
        if (!isPaused) SetMatchPausedFlags();
        if (wasTemporaryFill)
        {
            PrintToAllChat($"{ChatColors.LightRed}PAUSED{ChatColors.Grey} · FILL PLAYER {ChatColors.White}{player.PlayerName}{ChatColors.Grey} LEFT");
            QueueMatchCoreEvent("fill_removed", expectedRevision => new
            {
                event_id = NextMatchCoreEventId("fill_removed"),
                event_type = "fill_removed",
                match_id = matchCoreId.Value.ToString(),
                expected_revision = expectedRevision,
                steam_id = steamId,
            });
            return;
        }

        PrintToAllChat($"{ChatColors.LightRed}PAUSED{ChatColors.Grey} · WAITING FOR {ChatColors.White}{player.PlayerName}");
        QueueMatchCoreEvent("player_disconnected", expectedRevision => new
        {
            event_id = NextMatchCoreEventId("disconnect"),
            event_type = "player_disconnected",
            match_id = matchCoreId.Value.ToString(),
            expected_revision = expectedRevision,
            steam_id = steamId,
            reconnect_window_seconds = legacyXMatchCoreReconnectWindow.Value,
        });

        if (matchCoreFillTimers.TryGetValue(steamId, out var existingFillTimer)) existingFillTimer.Kill();
        matchCoreFillTimers[steamId] = AddTimer(legacyXMatchCoreFillTimeout.Value, () =>
        {
            if (matchCoreSlotsByOriginal.TryGetValue(steamId, out var current) && current.ActiveSteamId == null)
            {
                PrintToAllChat("STILL WAITING · STAFF CAN ADD A FILL PLAYER");
            }
        });
    }

    private void OnLegacyXMatchCoreConnect(CCSPlayerController player)
    {
        if (!IsMatchCoreEnabled || !matchCoreId.HasValue) return;
        OnLegacyXRankReconnect(player);
        var steamId = CoreSteamId(player);
        if (!matchCoreSlotsByOriginal.TryGetValue(steamId, out var slot)) return;
        if (slot.ActiveSteamId == steamId) return;

        var outgoingFillSteamId = slot.IsFill ? slot.ActiveSteamId : null;
        if (!string.IsNullOrWhiteSpace(outgoingFillSteamId))
        {
            var fill = Utilities.GetPlayers().FirstOrDefault(candidate => IsPlayerValid(candidate) && CoreSteamId(candidate) == outgoingFillSteamId);
            if (fill != null)
            {
                fill.ChangeTeam(CsTeam.Spectator);
                PrintToPlayerChat(fill, "PLAYER IS BACK · YOU ARE NOW A SPECTATOR · THANKS");
            }
        }
        if (matchCoreFillTimers.TryGetValue(steamId, out var fillTimer)) fillTimer.Kill();
        matchCoreFillTimers.Remove(steamId);
        slot.ActiveSteamId = steamId;
        slot.IsFill = false;
        QueueMatchCoreEvent("player_returned", expectedRevision => new
        {
            event_id = NextMatchCoreEventId("return"),
            event_type = "player_returned",
            match_id = matchCoreId.Value.ToString(),
            expected_revision = expectedRevision,
            steam_id = steamId,
        }, _ => RestoreMatchCoreSnapshot(player, steamId));
    }

    private void OnLegacyXMatchCoreResumed()
    {
        if (!IsMatchCoreEnabled || !matchCoreId.HasValue) return;
        QueueMatchCoreEvent("state_transition_resume", expectedRevision => new
        {
            event_id = NextMatchCoreEventId("resume"),
            event_type = "state_transition",
            match_id = matchCoreId.Value.ToString(),
            expected_revision = expectedRevision,
            state = "LIVE",
        });
    }

    /// <param name="winnerTeam">MatchZy winner key ("team1" = matchzyTeam1, or "none").</param>
    /// <param name="mapTeam1Rounds">Rounds won on this map by matchzyTeam1.</param>
    /// <param name="mapTeam2Rounds">Rounds won on this map by matchzyTeam2.</param>
    private void OnLegacyXMatchCoreFinal(string winnerTeam, int team1Score, int team2Score, int mapTeam1Rounds, int mapTeam2Rounds)
    {
        if (!IsMatchCoreEnabled || !matchCoreId.HasValue) return;
        // Match Core keys teams by starting side, MatchZy by config order; the result uses Match Core keys.
        var coreWinner = CoreTeamFromMatchzy(winnerTeam);
        var coreTeam1Series = rankCoreTeam1IsMatchzyTeam1 ? team1Score : team2Score;
        var coreTeam2Series = rankCoreTeam1IsMatchzyTeam1 ? team2Score : team1Score;
        var rewardEligible = !matchCoreSlotsByOriginal.Values.Any(slot => slot.IsFill) && coreWinner is "team1" or "team2";
        // The API decides whether the match is ranked and what everyone gets; the plugin only reports raw telemetry.
        var competitiveResult = BuildCompetitiveResult(mapTeam1Rounds, mapTeam2Rounds, finishedNormally: true);
        QueueMatchCoreEvent("result_final", expectedRevision => new
        {
            event_id = NextMatchCoreEventId("final"),
            event_type = "result_final",
            match_id = matchCoreId.Value.ToString(),
            expected_revision = expectedRevision,
            result = new
            {
                winner_team = coreWinner,
                team1_series_score = coreTeam1Series,
                team2_series_score = coreTeam2Series,
                map_name = Server.MapName,
                map_number = matchConfig.CurrentMapNumber,
                reward_eligible = rewardEligible,
                competitive_result = competitiveResult,
            },
        });
    }

    private bool CanLegacyXMatchCoreResume(out string reason)
    {
        if (!IsMatchCoreEnabled || !matchCoreId.HasValue)
        {
            reason = string.Empty;
            return true;
        }
        if (matchCoreSlotsByOriginal.Count != 10 || matchCoreSlotsByOriginal.Values.Any(slot => string.IsNullOrWhiteSpace(slot.ActiveSteamId)))
        {
            reason = "ALL 10 PLAYERS MUST BE BACK TO RESUME";
            return false;
        }
        var (ctPlayers, _) = GetTeamPlayerCount((int)CsTeam.CounterTerrorist, false);
        var (tPlayers, _) = GetTeamPlayerCount((int)CsTeam.Terrorist, false);
        if (ctPlayers != 5 || tPlayers != 5)
        {
            reason = "5 CT AND 5 T NEEDED TO RESUME";
            return false;
        }
        if (!matchCoreRemoteSlotsReady)
        {
            reason = "ONE MOMENT · TRY AGAIN";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    [ConsoleCommand("css_legacyx_fill", "Assign a connected player as a temporary Match Core fill: css_legacyx_fill <steamid64> <team1|team2> <slot 1-5>")]
    public void OnLegacyXMatchCoreFillCommand(CCSPlayerController? player, CommandInfo? command)
    {
        if (player != null && !IsPlayerAdmin(player, "css_legacyx_fill", "@css/config"))
        {
            SendPlayerNotAdminMessage(player);
            return;
        }
        if (!IsMatchCoreEnabled || !matchCoreId.HasValue || command == null || command.ArgCount < 4)
        {
            ReplyToUserCommand(player, "Usage: css_legacyx_fill <steamid64> <team1|team2> <slot 1-5>");
            return;
        }
        var steamId = command.ArgByIndex(1).Trim();
        var teamKey = command.ArgByIndex(2).Trim().ToLowerInvariant();
        if (!int.TryParse(command.ArgByIndex(3), out var slotIndex) || !new[] { "team1", "team2" }.Contains(teamKey) || slotIndex is < 1 or > 5 || !ulong.TryParse(steamId, out _))
        {
            ReplyToUserCommand(player, "{lightred}CHECK THE FILL DETAILS");
            return;
        }
        if (matchCoreSlotsByOriginal.ContainsKey(steamId))
        {
            ReplyToUserCommand(player, "{lightred}THIS PLAYER IS ALREADY IN THE MATCH");
            return;
        }
        var slot = matchCoreSlotsByOriginal.Values.FirstOrDefault(candidate => candidate.TeamKey == teamKey && candidate.SlotIndex == slotIndex);
        var fill = Utilities.GetPlayers().FirstOrDefault(candidate => IsPlayerValid(candidate) && CoreSteamId(candidate) == steamId);
        if (slot == null || slot.ActiveSteamId != null || fill == null)
        {
            ReplyToUserCommand(player, "{lightred}SLOT TAKEN OR PLAYER NOT HERE");
            return;
        }

        fill.ChangeTeam(teamKey == "team1" ? CsTeam.CounterTerrorist : CsTeam.Terrorist);
        slot.ActiveSteamId = steamId;
        slot.IsFill = true;
        matchCoreRemoteSlotsReady = false;
        QueueMatchCoreEvent("fill_assigned", expectedRevision => new
        {
            event_id = NextMatchCoreEventId("fill"),
            event_type = "fill_assigned",
            match_id = matchCoreId.Value.ToString(),
            expected_revision = expectedRevision,
            steam_id = steamId,
            name = fill.PlayerName,
            team_key = teamKey,
            slot_index = slotIndex,
        });
        PrintToAllChat($"{ChatColors.White}{fill.PlayerName}{ChatColors.Grey} FILLS IN · NO RANK OR EXP");
    }

    private CoreSnapshot? CaptureMatchCoreSnapshot(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return null;
        var origin = pawn.AbsOrigin;
        if (origin == null) return null;
        var angle = pawn.EyeAngles;
        var weapons = pawn.WeaponServices?.MyWeapons
            .Where(handle => handle.IsValid && handle.Value != null && !string.IsNullOrWhiteSpace(handle.Value.DesignerName))
            .Select(handle => handle.Value!.DesignerName)
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToArray() ?? Array.Empty<string>();
        return new CoreSnapshot
        {
            Health = Math.Clamp(pawn.Health, 1, 100),
            Armor = Math.Clamp(pawn.ArmorValue, 0, 100),
            Weapons = weapons,
            X = origin.X,
            Y = origin.Y,
            Z = origin.Z,
            Pitch = angle.X,
            Yaw = angle.Y,
            Roll = angle.Z,
        };
    }

    private void RestoreMatchCoreSnapshot(CCSPlayerController player, string steamId)
    {
        if (!matchCoreSnapshots.TryGetValue(steamId, out var snapshot) || !IsPlayerValid(player)) return;
        AddTimer(0.5f, () =>
        {
            if (!IsPlayerValid(player)) return;
            player.Respawn();
            Server.NextFrame(() =>
            {
                var pawn = player.PlayerPawn.Value;
                if (pawn == null || !pawn.IsValid) return;
                player.RemoveWeapons();
                foreach (var weapon in snapshot.Weapons.Where(weapon => weapon.StartsWith("weapon_", StringComparison.Ordinal) && weapon != "weapon_c4"))
                {
                    player.GiveNamedItem(weapon);
                }
                pawn.Health = snapshot.Health;
                pawn.ArmorValue = snapshot.Armor;
                pawn.Teleport(new Vector(snapshot.X, snapshot.Y, snapshot.Z), new QAngle(snapshot.Pitch, snapshot.Yaw, snapshot.Roll), new Vector(0, 0, 0));
            });
        });
    }

    private string NextMatchCoreEventId(string type)
    {
        matchCoreEventSequence++;
        return $"legacyx-core:{matchCoreId!.Value:N}:{matchCoreEventSequence}:{type}";
    }

    private void QueueMatchCoreEvent(string eventName, Func<int, object> payloadFactory, Action<JsonElement>? onSuccess = null)
    {
        lock (matchCoreQueueLock)
        {
            matchCoreQueue = matchCoreQueue.ContinueWith(async _ =>
            {
                var payload = payloadFactory(matchCoreRevision);
                await PostMatchCoreEventAsync(eventName, payload, onSuccess);
            }, TaskScheduler.Default).Unwrap();
        }
    }

    private async Task PostMatchCoreEventAsync(string eventName, object payload, Action<JsonElement>? onSuccess)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, legacyXMatchCoreApiUrl.Value.Trim());
            request.Headers.Add("x-plugin-id", "legacyx-match-core");
            request.Headers.Add("x-plugin-secret", legacyXMatchCorePluginSecret.Value.Trim());
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await matchCoreHttp.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Log($"[LegacyXMatchCore] {eventName} rejected ({(int)response.StatusCode}): {responseBody[..Math.Min(240, responseBody.Length)]}");
                return;
            }
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("result", out var result)) return;
            if (result.TryGetProperty("revision", out var revision) && revision.TryGetInt32(out var parsedRevision)) matchCoreRevision = parsedRevision;
            if (result.TryGetProperty("slots_ready", out var slotsReady) && slotsReady.ValueKind is JsonValueKind.True or JsonValueKind.False) matchCoreRemoteSlotsReady = slotsReady.GetBoolean();
            onSuccess?.Invoke(result);
            Log($"[LegacyXMatchCore] {eventName} accepted for {matchCoreId} at revision {matchCoreRevision}.");
        }
        catch (Exception exception)
        {
            Log($"[LegacyXMatchCore] {eventName} transport error: {exception.Message}");
        }
    }
}
