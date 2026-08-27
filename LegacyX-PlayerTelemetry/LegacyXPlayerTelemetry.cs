using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using LegacyX.Shared.Configuration;

namespace LegacyX.PlayerTelemetry;

public sealed class LegacyXPlayerTelemetryConfig : BasePluginConfig
{
    public bool Enabled { get; set; } = true;
    public string ApiBaseUrl { get; set; } = "";
    public string PluginId { get; set; } = "legacyx-player-telemetry";
    public string PluginSecret { get; set; } = "";
    public string ServerId { get; set; } = "legacyx-match-1";
    public string ServerMode { get; set; } = "competitive_5v5";
    public bool RoundSummaryEnabled { get; set; } = false;
}

internal sealed class PlayerSession
{
    public required ulong SteamId { get; init; }
    public required string PlayerName { get; init; }
    public required DateTimeOffset ConnectedAt { get; init; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int DamageDealt { get; set; }
    public int DamageTaken { get; set; }
}

public sealed class LegacyXPlayerTelemetry : BasePlugin, IPluginConfig<LegacyXPlayerTelemetryConfig>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly Dictionary<ulong, PlayerSession> _sessions = new();
    private readonly Dictionary<ulong, int> _lastRoundSummaryExperience = new();
    private int _roundNumber;
    private string _matchReference = "";
    public required LegacyXPlayerTelemetryConfig Config { get; set; }

    public override string ModuleAuthor => "LEGACY-X Community";
    public override string ModuleName => "LEGACY-X Player Telemetry";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    public void OnConfigParsed(LegacyXPlayerTelemetryConfig config)
    {
        var environment = LegacyXEnvironmentLoader.Load();
        config.Enabled = environment.GetModuleBoolean("PLAYER_TELEMETRY", "ENABLED", config.Enabled);
        config.ApiBaseUrl = environment.Get("LEGACYX_API_BASE_URL", config.ApiBaseUrl);
        config.PluginId = environment.GetModule("PLAYER_TELEMETRY", "PLUGIN_ID", config.PluginId);
        config.PluginSecret = environment.GetModule("PLAYER_TELEMETRY", "PLUGIN_TOKEN", config.PluginSecret);
        config.ServerId = environment.Get("LEGACYX_SERVER_ID", config.ServerId);
        config.ServerMode = environment.Get("LEGACYX_SERVER_MODE", config.ServerMode);
        config.RoundSummaryEnabled = environment.GetModuleBoolean("PLAYER_TELEMETRY", "ROUND_SUMMARY_ENABLED", config.RoundSummaryEnabled);
        Config = config;
        Config.ApiBaseUrl = Config.ApiBaseUrl.TrimEnd('/');
        Config.ServerId = Config.ServerId.Trim();
        Config.ServerMode = Config.ServerMode.Trim().ToLowerInvariant();
    }

    public override void Load(bool hotReload)
    {
        ResetMatchReference();
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        Console.WriteLine($"[{ModuleName}] Loaded — privacy-safe match telemetry is ready.");
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!IsTrackable(player)) return HookResult.Continue;
        _sessions[player!.SteamID] = new PlayerSession
        {
            SteamId = player.SteamID,
            PlayerName = player.PlayerName ?? "",
            ConnectedAt = DateTimeOffset.UtcNow,
        };
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!IsTrackable(player)) return HookResult.Continue;
        if (_sessions.Remove(player!.SteamID, out var session))
        {
            _lastRoundSummaryExperience.Remove(player.SteamID);
            _ = SendSnapshotAsync(session, "player_disconnected", "client_disconnect", "engine_disconnect_reason_unavailable");
        }
        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _roundNumber++;
        if (_roundNumber == 1) ResetMatchReference();
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        var roundNumber = _roundNumber;
        foreach (var session in _sessions.Values)
        {
            _ = SendSnapshotAsync(session, "round_snapshot", null, null, roundNumber);
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        var attacker = @event.Attacker;
        if (IsTrackable(victim) && _sessions.TryGetValue(victim!.SteamID, out var victimSession)) victimSession.Deaths++;
        if (IsTrackable(attacker) && attacker != victim && _sessions.TryGetValue(attacker!.SteamID, out var attackerSession)) attackerSession.Kills++;
        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var attacker = @event.Attacker;
        var victim = @event.Userid;
        var damage = Math.Max(0, @event.DmgHealth);
        if (damage == 0) return HookResult.Continue;
        if (IsTrackable(attacker) && attacker != victim && _sessions.TryGetValue(attacker!.SteamID, out var attackerSession)) attackerSession.DamageDealt += damage;
        if (IsTrackable(victim) && _sessions.TryGetValue(victim!.SteamID, out var victimSession)) victimSession.DamageTaken += damage;
        return HookResult.Continue;
    }

    private async Task SendSnapshotAsync(PlayerSession session, string eventType, string? disconnectMethod, string? disconnectReason, int? roundNumberOverride = null)
    {
        if (!Ready()) return;
        var activeSeconds = Math.Max(0, (int)(DateTimeOffset.UtcNow - session.ConnectedAt).TotalSeconds);
        var payload = new Dictionary<string, object?>
        {
            ["event_id"] = $"telemetry-{Guid.NewGuid():N}",
            ["event_type"] = eventType,
            ["server_id"] = Config.ServerId,
            ["server_mode"] = Config.ServerMode,
            ["match_reference"] = _matchReference,
            ["map_name"] = Server.MapName ?? "",
            ["steam_id"] = session.SteamId.ToString(),
            ["player_name"] = session.PlayerName,
            ["round_number"] = roundNumberOverride ?? _roundNumber,
            ["match_state"] = "live",
            ["active_seconds"] = activeSeconds,
            ["disconnect_method"] = disconnectMethod,
            ["disconnect_reason"] = disconnectReason,
            ["metrics"] = new Dictionary<string, int>
            {
                ["kills"] = session.Kills,
                ["deaths"] = session.Deaths,
                ["damage_dealt"] = session.DamageDealt,
                ["damage_taken"] = session.DamageTaken,
            },
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.ApiBaseUrl}/api/v1/plugin/player-telemetry/events");
            request.Headers.Add("x-plugin-id", Config.PluginId);
            request.Headers.Add("x-plugin-secret", Config.PluginSecret);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) Console.WriteLine($"[{ModuleName}] Telemetry event rejected: {(int)response.StatusCode}");
            if (eventType == "round_snapshot" && Config.RoundSummaryEnabled && response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (TryReadRoundProgression(document.RootElement, out var experience, out var rankName))
                    PrintRoundSummary(session.SteamId, roundNumberOverride ?? _roundNumber, experience, rankName);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Telemetry delivery failed: {exception.Message}");
        }
    }

    private void ResetMatchReference()
    {
        _roundNumber = 0;
        _lastRoundSummaryExperience.Clear();
        _matchReference = $"{Config.ServerId}:{Server.MapName ?? "unknown"}:{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }

    private bool Ready() => Config.Enabled && !string.IsNullOrWhiteSpace(Config.ApiBaseUrl) && !string.IsNullOrWhiteSpace(Config.PluginSecret) && !string.IsNullOrWhiteSpace(Config.ServerId);
    private static bool IsTrackable(CCSPlayerController? player) => player != null && player.IsValid && !player.IsBot && !player.IsHLTV && player.SteamID > 0;

    private static bool TryReadRoundProgression(JsonElement root, out int experience, out string rankName)
    {
        experience = 0;
        rankName = "Unranked";
        if (!root.TryGetProperty("progression", out var progression) || progression.ValueKind != JsonValueKind.Object) return false;
        if (!progression.TryGetProperty("experience", out var experienceElement) || !experienceElement.TryGetInt32(out experience)) return false;
        if (progression.TryGetProperty("rankName", out var rankElement) && rankElement.ValueKind == JsonValueKind.String) rankName = rankElement.GetString() ?? rankName;
        return true;
    }

    private void PrintRoundSummary(ulong steamId, int roundNumber, int currentExperience, string rankName)
    {
        var hasPreviousExperience = _lastRoundSummaryExperience.TryGetValue(steamId, out var previousExperience);
        _lastRoundSummaryExperience[steamId] = currentExperience;
        var delta = hasPreviousExperience ? currentExperience - previousExperience : 0;
        var expColor = delta < 0 ? "{red}" : "{green}";
        var sign = delta >= 0 ? "+" : string.Empty;
        Server.NextFrame(() =>
        {
            var player = Utilities.GetPlayers().FirstOrDefault(candidate => candidate != null && candidate.IsValid && candidate.SteamID == steamId);
            if (player == null) return;
            player.PrintToChat("{green}LEGACY-X • {default}ROUND ENDED");
            player.PrintToChat("────────────────────────");
            player.PrintToChat($"ROUND: {roundNumber}");
            player.PrintToChat($"SERVER: {Config.ServerId}");
            player.PrintToChat($"EXP: {expColor}{sign}{delta}");
            player.PrintToChat($"RANK: {{green}}{rankName}");
        });
    }
}
