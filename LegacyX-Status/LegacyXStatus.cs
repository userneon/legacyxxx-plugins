using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using LegacyX.Shared.Configuration;

namespace LegacyXStatus;

/// <summary>
/// Game → API → database: every LEGACYX_STATUS_INTERVAL_SECONDS (30) the server reports who it is,
/// what it is playing and who is on it (POST /plugin/servers/heartbeat), plus the live score, round
/// and teams (POST /plugin/live-match/snapshots). The website's Play pages and home tiles, the
/// Discord boards and "playing now" on profiles all read these rows. Nothing is stored locally;
/// if the API is unreachable the server simply shows as offline until the next report arrives.
/// </summary>
public sealed class LegacyXStatus : BasePlugin
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null };
    // Snapshot revisions must only grow per server, across restarts too: seconds since 2026-01-01.
    private static readonly long RevisionEpoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    private StatusSettings settings = StatusSettings.Disabled;
    private int sending;

    public override string ModuleAuthor => "LEGACY-X";
    public override string ModuleName => "LEGACY-X Status";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    public override void Load(bool hotReload)
    {
        settings = StatusSettings.Load(LegacyXEnvironmentLoader.Load());
        if (!settings.Enabled)
        {
            Console.WriteLine($"[{ModuleName}] Disabled by central environment.");
            return;
        }
        if (settings.Missing.Count > 0)
        {
            Console.WriteLine($"[{ModuleName}] Not reporting: missing {string.Join(", ", settings.Missing)} in CounterStrikeSharp/.env.");
            return;
        }
        // An empty CS2 server hibernates: no frames, so no timers and no reports, and the site would
        // show it offline until someone joins. Keep it awake; server.cfg may reset the cvar per map.
        if (settings.KeepAwake)
        {
            KeepAwake();
            RegisterListener<Listeners.OnMapStart>(_ => KeepAwake());
        }
        // One repeating timer for the plugin's lifetime; it keeps running across map changes.
        AddTimer(settings.IntervalSeconds, Report, TimerFlags.REPEAT);
        Console.WriteLine($"[{ModuleName}] Reporting {settings.ServerId} every {settings.IntervalSeconds}s to {settings.ApiBaseUrl}.");
    }

    private static void KeepAwake() => Server.ExecuteCommand("sv_hibernate_when_empty 0");

    /// <summary>Runs on the game thread: read the server state, then send it in the background.</summary>
    private void Report()
    {
        if (Interlocked.Exchange(ref sending, 1) == 1) return; // the previous report is still on its way
        object heartbeat;
        object snapshot;
        try
        {
            (heartbeat, snapshot) = Capture();
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref sending, 0);
            Console.WriteLine($"[{ModuleName}] Could not read the server state: {exception.Message}");
            return;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await Post("/api/v1/plugin/servers/heartbeat", heartbeat);
                await Post("/api/v1/plugin/live-match/snapshots", snapshot);
            }
            finally
            {
                Interlocked.Exchange(ref sending, 0);
            }
        });
    }

    private (object Heartbeat, object Snapshot) Capture()
    {
        var humans = Utilities.GetPlayers()
            .Where(player => player is { IsValid: true, IsBot: false, IsHLTV: false } && player.Connected == PlayerConnectedState.PlayerConnected && player.SteamID > 76561197960265728UL)
            .ToList();
        var map = Server.MapName ?? "";
        var name = settings.ServerName.Length > 0 ? settings.ServerName : (ConVar.Find("hostname")?.StringValue ?? settings.ServerId);

        var heartbeat = new
        {
            serverId = settings.ServerId,
            name = Clip(name, 100),
            address = settings.ServerAddress,
            gotvAddress = settings.GotvAddress.Length > 0 ? settings.GotvAddress : null,
            map = Clip(map, 64),
            mode = settings.ServerMode,
            maxPlayers = Math.Clamp(Server.MaxPlayers, 1, 128),
            players = humans.Select(player => new { steamId = player.SteamID.ToString(), name = Clip(player.PlayerName, 128) }).ToList(),
        };

        var rules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        int? scoreT = null;
        int? scoreCt = null;
        foreach (var team in Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager"))
        {
            if (team.TeamNum == (int)CsTeam.Terrorist) scoreT = team.Score;
            else if (team.TeamNum == (int)CsTeam.CounterTerrorist) scoreCt = team.Score;
        }
        var state = humans.Count == 0 || rules == null || rules.WarmupPeriod ? "waiting"
            : rules.GamePhase == 5 ? "ended"
            : rules.MatchWaitingForResume || rules.TerroristTimeOutActive || rules.CTTimeOutActive ? "paused"
            : "live";

        object Player(CCSPlayerController player)
        {
            var stats = player.ActionTrackingServices?.MatchStats;
            return new
            {
                steam_id = player.SteamID.ToString(),
                name = Clip(player.PlayerName.Length > 0 ? player.PlayerName : "Player", 128),
                connected = true,
                ping = (int)Math.Min(player.Ping, 1000u),
                kills = stats == null ? (int?)null : Math.Clamp(stats.Kills, 0, 999),
                deaths = stats == null ? (int?)null : Math.Clamp(stats.Deaths, 0, 999),
                assists = stats == null ? (int?)null : Math.Clamp(stats.Assists, 0, 999),
            };
        }

        var revision = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - RevisionEpoch;
        var snapshot = new
        {
            event_id = $"status:{Safe(settings.ServerId)}:{revision}",
            server_id = settings.ServerId,
            live_match = new
            {
                schema_version = 1,
                snapshot_revision = revision,
                captured_at = DateTimeOffset.UtcNow.ToString("O"),
                state,
                map_name = Clip(map, 128),
                round_number = rules == null ? (int?)null : Math.Clamp(rules.TotalRoundsPlayed + (state == "live" ? 1 : 0), 0, 500),
                score_t = scoreT is null ? (int?)null : Math.Clamp(scoreT.Value, 0, 500),
                score_ct = scoreCt is null ? (int?)null : Math.Clamp(scoreCt.Value, 0, 500),
                terrorist_players = humans.Where(p => p.TeamNum == (int)CsTeam.Terrorist).Take(16).Select(Player).ToList(),
                counter_terrorist_players = humans.Where(p => p.TeamNum == (int)CsTeam.CounterTerrorist).Take(16).Select(Player).ToList(),
                spectator_players = humans.Where(p => p.TeamNum != (int)CsTeam.Terrorist && p.TeamNum != (int)CsTeam.CounterTerrorist).Take(64).Select(Player).ToList(),
            },
        };
        return (heartbeat, snapshot);
    }

    private async Task Post(string path, object body)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.ApiBaseUrl + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
            request.Headers.Add("x-plugin-id", settings.PluginId);
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                // Status code only: the body could echo request details, and the token is never logged.
                Console.WriteLine($"[{ModuleName}] {path} answered {(int)response.StatusCode}.");
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] {path} unreachable: {exception.GetType().Name}.");
        }
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? "").Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static string Safe(string value) => new(value.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '-').ToArray());
}

/// <summary>Settings from CounterStrikeSharp/.env. Missing required values keep the plugin quiet.</summary>
public sealed record StatusSettings(
    bool Enabled,
    string ApiBaseUrl,
    string PluginId,
    string Token,
    string ServerId,
    string ServerAddress,
    string ServerName,
    string GotvAddress,
    string ServerMode,
    int IntervalSeconds,
    bool KeepAwake,
    IReadOnlyList<string> Missing)
{
    public static readonly StatusSettings Disabled = new(false, "", "", "", "", "", "", "", "", 30, false, Array.Empty<string>());

    public static StatusSettings Load(LegacyXEnvironment environment)
    {
        var apiBaseUrl = environment.Get("LEGACYX_API_BASE_URL").Trim().TrimEnd('/');
        var token = environment.GetModule("STATUS", "PLUGIN_TOKEN").Trim();
        var serverId = environment.Get("LEGACYX_SERVER_ID").Trim();
        var address = environment.Get("LEGACYX_SERVER_ADDRESS").Trim();
        var missing = new List<string>();
        if (!apiBaseUrl.StartsWith("https://", StringComparison.Ordinal) && !apiBaseUrl.StartsWith("http://127.0.0.1", StringComparison.Ordinal)) missing.Add("LEGACYX_API_BASE_URL");
        if (token.Length < 20) missing.Add("LEGACYX_STATUS_PLUGIN_TOKEN");
        if (!System.Text.RegularExpressions.Regex.IsMatch(serverId, "^[A-Za-z0-9._:-]{1,64}$") || serverId.StartsWith("REPLACE", StringComparison.Ordinal)) missing.Add("LEGACYX_SERVER_ID");
        if (address.Length == 0 || address.StartsWith("REPLACE", StringComparison.Ordinal)) missing.Add("LEGACYX_SERVER_ADDRESS");
        return new StatusSettings(
            environment.GetModuleBoolean("STATUS", "ENABLED", true),
            apiBaseUrl,
            // The live snapshot route only accepts this identity.
            environment.GetModule("STATUS", "PLUGIN_ID", "legacyx-live-snapshot").Trim(),
            token,
            serverId,
            address,
            environment.Get("LEGACYX_SERVER_NAME").Trim(),
            environment.Get("LEGACYX_SERVER_GOTV_ADDRESS").Trim(),
            environment.Get("LEGACYX_SERVER_MODE", "competitive_5v5").Trim(),
            environment.GetModuleInt("STATUS", "INTERVAL_SECONDS", 30, 10, 60),
            environment.GetModuleBoolean("STATUS", "KEEP_AWAKE", true),
            missing);
    }
}
