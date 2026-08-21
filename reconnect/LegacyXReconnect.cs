using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;

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
    public int HeartbeatSeconds { get; set; } = 30;
    public string ChatPrefix { get; set; } = "{Lime}[LEGACY-X]{Default}";
}

public sealed class LegacyXReconnect : BasePlugin, IPluginConfig<LegacyXReconnectConfig>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly Dictionary<ulong, Guid> _sessions = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _heartbeatTimer;
    public required LegacyXReconnectConfig Config { get; set; }

    public override string ModuleAuthor => "LEGACY-X Community";
    public override string ModuleName => "LEGACY-X Reconnect";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    public void OnConfigParsed(LegacyXReconnectConfig config)
    {
        Config = config;
        Config.ApiBaseUrl = Config.ApiBaseUrl.TrimEnd('/');
        Config.ServerId = Config.ServerId.Trim();
        Config.ServerAddress = Config.ServerAddress.Trim();
        Config.HeartbeatSeconds = Math.Clamp(Config.HeartbeatSeconds, 10, 120);
    }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        _heartbeatTimer = AddTimer(Config.HeartbeatSeconds, SendHeartbeat, TimerFlags.REPEAT);
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
        _ = SendEventAsync(new Dictionary<string, object?>
        {
            ["event"] = "server_heartbeat",
            ["event_id"] = $"heartbeat-{Config.ServerId}-{Guid.NewGuid():N}",
            ["server_id"] = Config.ServerId,
            ["server_address"] = Config.ServerAddress,
            ["map_name"] = Server.MapName ?? "",
            ["mode"] = Config.ServerMode,
            ["player_count"] = playerCount,
        });
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
            Print(player, "Reconnecting to your last online LEGACY-X server…");
            Server.NextFrame(() => { if (player.IsValid) player.ExecuteClientCommand($"connect {address}"); });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Reconnect lookup failed: {exception.Message}");
            Print(player, "Last Played session is temporarily unavailable.");
        }
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
    private void Print(CCSPlayerController player, string message) => Server.NextFrame(() => { if (player.IsValid) player.PrintToChat($"{Config.ChatPrefix} {message}"); });
}
