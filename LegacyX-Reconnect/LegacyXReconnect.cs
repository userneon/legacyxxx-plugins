using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using LegacyX.Shared.Configuration;

namespace LegacyXReconnect;

public sealed class LegacyXReconnectConfig : BasePluginConfig
{
    public bool Enabled { get; set; } = true;
    public string ApiBaseUrl { get; set; } = "";
    public string PluginId { get; set; } = "legacyx-reconnect";
    public string PluginSecret { get; set; } = "";
    public string ServerId { get; set; } = "legacyx-match-1";
    public string ServerAddress { get; set; } = "";
    public string ServerMode { get; set; } = "competitive_5v5";
    /// <summary>GOTV address (ip:port) for "Spectate" on the website; empty when GOTV is off.</summary>
    public string GotvAddress { get; set; } = "";
    public int HeartbeatSeconds { get; set; } = 30;
    /// <summary>Send kills to the website's live kill feed (held in API memory only, never stored).</summary>
    public bool KillFeedEnabled { get; set; } = true;
    public string ChatPrefix { get; set; } = LegacyXChat.Prefix;
}

public sealed class LegacyXReconnect : BasePlugin, IPluginConfig<LegacyXReconnectConfig>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly Dictionary<ulong, Guid> _sessions = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _heartbeatTimer;
    private static readonly Regex KillWeaponPattern = new("[^A-Za-z0-9_ -]", RegexOptions.Compiled);
    private readonly List<Dictionary<string, object?>> _pendingKills = new();
    public required LegacyXReconnectConfig Config { get; set; }

    public override string ModuleAuthor => "LEGACY-X Community";
    public override string ModuleName => "LEGACY-X Reconnect";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    public void OnConfigParsed(LegacyXReconnectConfig config)
    {
        var environment = LegacyXEnvironmentLoader.Load();
        config.Enabled = environment.GetModuleBoolean("RECONNECT", "ENABLED", config.Enabled);
        config.ApiBaseUrl = environment.Get("LEGACYX_API_BASE_URL", config.ApiBaseUrl);
        config.PluginId = environment.GetModule("RECONNECT", "PLUGIN_ID", config.PluginId);
        config.PluginSecret = environment.GetModule("RECONNECT", "PLUGIN_TOKEN", config.PluginSecret);
        config.ServerId = environment.Get("LEGACYX_SERVER_ID", config.ServerId);
        config.ServerAddress = environment.Get("LEGACYX_SERVER_ADDRESS", config.ServerAddress);
        config.ServerMode = environment.Get("LEGACYX_SERVER_MODE", config.ServerMode);
        config.GotvAddress = environment.Get("LEGACYX_GOTV_ADDRESS", config.GotvAddress);
        config.KillFeedEnabled = environment.GetModuleBoolean("RECONNECT", "KILLFEED_ENABLED", config.KillFeedEnabled);
        Config = config;
        Config.ApiBaseUrl = Config.ApiBaseUrl.TrimEnd('/');
        Config.ServerId = Config.ServerId.Trim();
        Config.ServerAddress = Config.ServerAddress.Trim();
        Config.ServerMode = Config.ServerMode.Trim().ToLowerInvariant();
        Config.GotvAddress = Config.GotvAddress.Trim();
        Config.HeartbeatSeconds = Math.Clamp(Config.HeartbeatSeconds, 10, 120);
    }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        _heartbeatTimer = AddTimer(Config.HeartbeatSeconds, SendHeartbeat, TimerFlags.REPEAT);
        if (Config.KillFeedEnabled)
        {
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            AddTimer(2.0f, FlushKills, TimerFlags.REPEAT);
        }
        Console.WriteLine($"[{ModuleName}] Loaded — session tracking and Last Played are ready.");
    }

    public override void Unload(bool hotReload)
    {
        _heartbeatTimer?.Kill();
        _heartbeatTimer = null;
    }

    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!IsTrackable(player)) return HookResult.Continue;
        var sessionId = Guid.NewGuid();
        _sessions[player!.SteamID] = sessionId;
        _ = SendPlayerEventAsync("player_connected", player, sessionId, "");
        return HookResult.Continue;
    }

    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!IsTrackable(player)) return HookResult.Continue;
        if (_sessions.Remove(player!.SteamID, out var sessionId))
        {
            _ = SendPlayerEventAsync("player_disconnected", player, sessionId, "disconnect");
        }
        return HookResult.Continue;
    }

    [ConsoleCommand("css_reconnect", "Reconnect to your last LEGACY-X server when it is online")]
    public void OnReconnect(CCSPlayerController? player, CommandInfo? command)
    {
        if (player == null || !player.IsValid) return;
        if (!Ready())
        {
            Print(player, "Reconnect is not configured on this server yet.");
            return;
        }
        _ = ReconnectToLastServerAsync(player);
    }

    private void SendHeartbeat()
    {
        if (!Ready()) return;
        var playerCount = Utilities.GetPlayers().Count(player => IsTrackable(player));
        var payload = new Dictionary<string, object?>
        {
            ["event"] = "server_heartbeat",
            ["event_id"] = $"heartbeat-{Config.ServerId}-{Guid.NewGuid():N}",
            ["server_id"] = Config.ServerId,
            ["server_address"] = Config.ServerAddress,
            ["map_name"] = Server.MapName ?? "",
            ["mode"] = Config.ServerMode,
            ["player_count"] = playerCount,
            // Capacity and GOTV feed the website's Play pages (slots, "Hide full", Spectate).
            ["max_players"] = Math.Clamp(Server.MaxPlayers, 1, 128),
        };
        if (!string.IsNullOrWhiteSpace(Config.GotvAddress)) payload["gotv_address"] = Config.GotvAddress;
        _ = SendEventAsync(payload);
    }

    private async Task SendPlayerEventAsync(string eventName, CCSPlayerController player, Guid sessionId, string disconnectReason)
    {
        if (!Ready()) return;
        await SendEventAsync(new Dictionary<string, object?>
        {
            ["event"] = eventName,
            ["event_id"] = $"{eventName}-{Config.ServerId}-{sessionId:N}",
            ["session_id"] = sessionId,
            ["steam_id"] = player.SteamID.ToString(),
            ["player_name"] = player.PlayerName ?? "",
            ["server_id"] = Config.ServerId,
            ["server_address"] = Config.ServerAddress,
            ["map_name"] = Server.MapName ?? "",
            ["mode"] = Config.ServerMode,
            ["disconnect_reason"] = disconnectReason,
        });
    }

    private async Task ReconnectToLastServerAsync(CCSPlayerController player)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Config.ApiBaseUrl}/api/v1/plugin/reconnect/players/{player.SteamID}?exclude_server_id={Uri.EscapeDataString(Config.ServerId)}");
            request.Headers.Add("x-plugin-id", Config.PluginId);
            request.Headers.Add("x-plugin-secret", Config.PluginSecret);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Print(player, "Last Played session is temporarily unavailable.");
                return;
            }
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var sessions = document.RootElement.GetProperty("sessions");
            JsonElement? target = null;
            foreach (var session in sessions.EnumerateArray())
            {
                if (session.TryGetProperty("reconnectable", out var reconnectable) && reconnectable.GetBoolean())
                {
                    target = session;
                    break;
                }
            }
            if (target is null || !target.Value.TryGetProperty("connect_address", out var addressElement))
            {
                Print(player, "No online previous LEGACY-X server is available to reconnect.");
                return;
            }
            var address = addressElement.GetString() ?? "";
            if (!Regex.IsMatch(address, "^[a-zA-Z0-9.-]+:[0-9]{2,5}$"))
            {
                Print(player, "Reconnect target validation failed.");
                return;
            }
            Print(player, $"IP: CONNECT {address}");
            Server.NextFrame(() => { if (player.IsValid) player.ExecuteClientCommand($"connect {address}"); });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Reconnect lookup failed: {exception.Message}");
            Print(player, "Last Played session is temporarily unavailable.");
        }
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        var attacker = @event.Attacker;
        // World damage, suicides and team switches are not kills.
        if (victim == null || !victim.IsValid || attacker == null || !attacker.IsValid || attacker == victim) return HookResult.Continue;
        if (_pendingKills.Count >= 50) return HookResult.Continue;
        var weapon = KillWeaponPattern.Replace(@event.Weapon ?? string.Empty, string.Empty).Trim();
        _pendingKills.Add(new Dictionary<string, object?>
        {
            ["event_id"] = $"kill-{Config.ServerId}-{Guid.NewGuid():N}",
            ["server_id"] = Config.ServerId,
            ["attacker_steam_id"] = attacker.IsBot || attacker.SteamID == 0 ? null : attacker.SteamID.ToString(),
            ["attacker_name"] = KillFeedName(attacker.PlayerName),
            ["victim_steam_id"] = victim.IsBot || victim.SteamID == 0 ? null : victim.SteamID.ToString(),
            ["victim_name"] = KillFeedName(victim.PlayerName),
            ["weapon"] = string.IsNullOrEmpty(weapon) ? "world" : weapon[..Math.Min(64, weapon.Length)],
            ["headshot"] = @event.Headshot,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
        });
        return HookResult.Continue;
    }

    private static string KillFeedName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0) return "Player";
        return trimmed.Length > 64 ? trimmed[..64] : trimmed;
    }

    /// <summary>Sends buffered kills in one request every two seconds.</summary>
    private void FlushKills()
    {
        if (_pendingKills.Count == 0 || !Ready()) return;
        var batch = _pendingKills.ToArray();
        _pendingKills.Clear();
        _ = PostAsync("/api/v1/plugin/killfeed/events", batch);
    }

    private async Task PostAsync(string path, object payload)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.ApiBaseUrl}{path}");
            request.Headers.Add("x-plugin-id", Config.PluginId);
            request.Headers.Add("x-plugin-secret", Config.PluginSecret);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) Console.WriteLine($"[{ModuleName}] {path} rejected: {(int)response.StatusCode}");
        }
        catch (Exception exception) { Console.WriteLine($"[{ModuleName}] {path} delivery failed: {exception.Message}"); }
    }

    private async Task SendEventAsync(object payload)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.ApiBaseUrl}/api/v1/plugin/reconnect/events");
            request.Headers.Add("x-plugin-id", Config.PluginId);
            request.Headers.Add("x-plugin-secret", Config.PluginSecret);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) Console.WriteLine($"[{ModuleName}] Event rejected: {(int)response.StatusCode}");
        }
        catch (Exception exception) { Console.WriteLine($"[{ModuleName}] Event delivery failed: {exception.Message}"); }
    }

    private bool Ready() => Config.Enabled && !string.IsNullOrWhiteSpace(Config.ApiBaseUrl) && !string.IsNullOrWhiteSpace(Config.PluginSecret) && !string.IsNullOrWhiteSpace(Config.ServerId) && !string.IsNullOrWhiteSpace(Config.ServerAddress);
    private static bool IsTrackable(CCSPlayerController? player) => player != null && player.IsValid && !player.IsBot && !player.IsHLTV && player.SteamID > 0;
    private void Print(CCSPlayerController player, string message) => Server.NextFrame(() =>
    {
        if (player.IsValid) player.PrintToChat(LegacyXChat.System(message));
    });
}
