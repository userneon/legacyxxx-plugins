using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using LegacyX.Shared.Configuration;

namespace AdminPlus;

/// <summary>
/// The website's staff console (Staff → Servers): every few seconds this server takes the actions queued for it, runs them as the
/// server console would, and reports how each went. The API cancels an action that waited more than two minutes, so nothing
/// queued while the server was down fires later. Server start and "restart all" need the host machine and are reported as such.
/// </summary>
public partial class AdminPlus
{
    private static readonly HttpClient StaffPanelHttp = new() { Timeout = TimeSpan.FromSeconds(5) };
    private string _staffPanelApi = "";
    private string _staffPanelToken = "";
    private string _staffPanelServerId = "";
    private bool _staffPanelBusy;
    private DateTime _staffPanelLastErrorUtc = DateTime.MinValue;

    private void InitializeStaffPanel()
    {
        var environment = LegacyXEnvironmentLoader.Load();
        if (!environment.GetModuleBoolean("ADMIN", "STAFF_PANEL_ENABLED", true)) return;
        _staffPanelApi = environment.GetModule("ADMIN", "API_BASE_URL").TrimEnd('/');
        _staffPanelToken = environment.GetModule("ADMIN", "PLUGIN_SECRET", environment.Get("LEGACYX_PLUGIN_TOKEN"));
        _staffPanelServerId = LegacyXServerRuntime.Load(environment).ServerId;
        if (string.IsNullOrWhiteSpace(_staffPanelApi) || string.IsNullOrWhiteSpace(_staffPanelToken) || !Regex.IsMatch(_staffPanelServerId, "^[A-Za-z0-9_-]{1,80}$"))
        {
            Console.WriteLine("[LEGACY-X Admin] Website staff console is off: the API address, token or server id is missing.");
            return;
        }
        var seconds = environment.GetModuleInt("ADMIN", "STAFF_PANEL_POLL_SECONDS", 3, 2, 30);
        AddTimer(seconds, PollStaffPanel, TimerFlags.REPEAT);
        Console.WriteLine($"[LEGACY-X Admin] Website staff console: taking actions for '{_staffPanelServerId}' every {seconds}s.");
    }

    private void PollStaffPanel()
    {
        if (_staffPanelBusy) return;
        _staffPanelBusy = true;
        _ = Task.Run(async () =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _staffPanelApi + "/api/v1/plugin/staffpanel/claim")
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { serverId = _staffPanelServerId }), Encoding.UTF8, "application/json"),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _staffPanelToken);
                using var response = await StaffPanelHttp.SendAsync(request);
                if (!response.IsSuccessStatusCode) { StaffPanelError($"claim returned {(int)response.StatusCode}"); return; }
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (!document.RootElement.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array) return;
                foreach (var action in actions.EnumerateArray())
                {
                    var id = action.GetProperty("id").GetString() ?? "";
                    var type = action.GetProperty("type").GetString() ?? "";
                    var payload = action.TryGetProperty("payload", out var p) ? p.Clone() : default;
                    var done = new TaskCompletionSource<(bool Ok, string Message)>();
                    Server.NextFrame(() =>
                    {
                        try { done.SetResult(RunStaffPanelAction(type, payload)); }
                        catch (Exception exception) { done.SetResult((false, $"Failed: {exception.Message}")); }
                    });
                    var (ok, message) = await done.Task;
                    Console.WriteLine($"[LEGACY-X Admin] Website staff console: {type} {(ok ? "done" : "failed")}: {message}");
                    await ReportStaffPanelAction(id, ok, message);
                }
            }
            catch (Exception exception)
            {
                StaffPanelError(exception.Message);
            }
            finally
            {
                _staffPanelBusy = false;
            }
        });
    }

    private async Task ReportStaffPanelAction(string id, bool ok, string message)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_staffPanelApi}/api/v1/plugin/staffpanel/actions/{Uri.EscapeDataString(id)}/result")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { serverId = _staffPanelServerId, ok, message = message.Length > 300 ? message[..300] : message }), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _staffPanelToken);
            using var response = await StaffPanelHttp.SendAsync(request);
            if (!response.IsSuccessStatusCode) StaffPanelError($"result returned {(int)response.StatusCode}");
        }
        catch (Exception exception)
        {
            StaffPanelError(exception.Message);
        }
    }

    private void StaffPanelError(string message)
    {
        if (DateTime.UtcNow - _staffPanelLastErrorUtc < TimeSpan.FromMinutes(5)) return;
        _staffPanelLastErrorUtc = DateTime.UtcNow;
        Console.WriteLine($"[LEGACY-X Admin] Website staff console: {message}");
    }

    private static string StaffText(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return "";
        // Typed text goes into console commands and chat: no quotes, semicolons or line breaks, and no colour codes.
        var clean = new string((value.GetString() ?? "").Where(c => c >= ' ' && c != '"' && c != ';' && c != '\u007F').ToArray()).Trim();
        return clean.Length > 240 ? clean[..240] : clean;
    }

    private static int StaffNumber(JsonElement payload, string name) =>
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static CCSPlayerController? StaffTarget(string steamId) =>
        Utilities.GetPlayers().FirstOrDefault(player => player.IsValid && !player.IsBot && player.SteamID.ToString() == steamId);

    /// <summary>Runs one console action on the main thread and says how it went.</summary>
    private (bool Ok, string Message) RunStaffPanelAction(string type, JsonElement payload)
    {
        var steamId = StaffText(payload, "playerSteamId");
        if (steamId.Length > 0 && !Regex.IsMatch(steamId, "^7656119\\d{10}$")) return (false, "That SteamID is not valid.");
        var message = StaffText(payload, "message");
        var target = steamId.Length > 0 ? StaffTarget(steamId) : null;
        // An online player is named by #userid, which every console command understands; an offline one by SteamID.
        var who = target != null ? $"#{target.UserId}" : steamId;
        switch (type)
        {
            case "ban":
            {
                var minutes = StaffText(payload, "banTerm") switch { "10m" => 10, "30m" => 30, "1h" => 60, "1d" => 1440, "7d" => 10080, _ => 0 };
                Server.ExecuteCommand($"css_ban {steamId} {minutes} \"{(message.Length > 0 ? message : "Banned by staff")}\"");
                return (true, minutes == 0 ? "Banned permanently." : $"Banned for {minutes} minutes.");
            }
            case "unban":
                Server.ExecuteCommand($"css_unban {steamId}");
                return (true, "Ban lifted.");
            case "mute":
            {
                var minutes = Math.Max(0, StaffNumber(payload, "durationSeconds") / 60);
                Server.ExecuteCommand($"css_mute {who} {minutes} \"{(message.Length > 0 ? message : "Muted by staff")}\"");
                return (true, minutes == 0 ? "Muted." : $"Muted for {minutes} minutes.");
            }
            case "kick":
                if (target == null) return (false, "The player is not on this server.");
                Server.ExecuteCommand($"css_kick {who} \"{(message.Length > 0 ? message : "Kicked by staff")}\"");
                return (true, $"Kicked {target.PlayerName}.");
            case "rename":
            {
                var name = StaffText(payload, "newName");
                if (target == null) return (false, "The player is not on this server.");
                if (name.Length < 2) return (false, "The new name is too short.");
                Server.ExecuteCommand($"css_rename {who} \"{name}\"");
                return (true, $"Renamed to {name}.");
            }
            case "map_change":
            {
                var map = StaffText(payload, "map");
                if (!Regex.IsMatch(map, "^[A-Za-z0-9_]{1,64}$")) return (false, "That map is not allowed.");
                Server.PrintToChatAll(LegacyXChat.System($"Changing the map to {{white}}{map}{{grey}} in 5 seconds."));
                AddTimer(5f, () => Server.ExecuteCommand($"changelevel {map}"));
                return (true, $"Changing to {map} in 5 seconds.");
            }
            case "server_announcement":
                Server.PrintToChatAll(LegacyXChat.System(message));
                return (true, "Announced in chat.");
            case "match_announcement":
                Server.PrintToChatAll(LegacyXChat.System(message));
                foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot)) player.PrintToCenter(message);
                return (true, "Announced in chat and on screen.");
            case "hud_announcement":
                foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot)) player.PrintToCenterAlert(message);
                return (true, "Shown on every screen.");
            case "player_hud_alert":
                if (target == null) return (false, "The player is not on this server.");
                target.PrintToCenterAlert(message);
                return (true, $"Shown to {target.PlayerName}.");
            case "player_message":
                if (target == null) return (false, "The player is not on this server.");
                target.PrintToChat(LegacyXChat.System(message));
                return (true, $"Sent to {target.PlayerName}.");
            case "player_ip_lookup":
                if (target == null) return (false, "The player is not on this server.");
                return (true, $"{target.PlayerName}: {(target.IpAddress ?? "unknown").Split(':')[0]}");
            case "timeout":
            {
                var seconds = Math.Clamp(StaffNumber(payload, "durationSeconds"), 10, 600);
                Server.ExecuteCommand("mp_pause_match");
                if (message.Length > 0) Server.PrintToChatAll(LegacyXChat.System(message));
                AddTimer(seconds, () => Server.ExecuteCommand("mp_unpause_match"));
                return (true, $"Paused for {seconds} seconds.");
            }
            case "unpause":
                Server.ExecuteCommand("mp_unpause_match");
                return (true, "Unpaused.");
            case "round_restart":
                Server.ExecuteCommand("mp_restartgame 1");
                return (true, "Round restarted.");
            case "stop_server":
                Server.PrintToChatAll(LegacyXChat.System("The server is shutting down in 10 seconds."));
                // A clean exit: systemd (Restart=on-failure) leaves the server stopped until it is started on the host.
                AddTimer(10f, () => Server.ExecuteCommand("quit"));
                return (true, "Stopping in 10 seconds. Start it again on the host: sudo systemctl start cs2@<port>.");
            case "restart_server":
                Server.PrintToChatAll(LegacyXChat.System("The server restarts in 10 seconds. Reconnect in a minute."));
                // Exiting with an error code makes systemd (Restart=on-failure) start the server again after 10 s.
                AddTimer(10f, () => Environment.Exit(75));
                return (true, "Restarting in 10 seconds; back in about a minute.");
            case "round_restore":
                return (false, "Restoring a round is done in game with MatchZy (.stop, or css_restore as staff).");
            case "start_server":
            case "restart_all":
                return (false, "This needs the host machine: sudo systemctl start cs2@<port> (or restart all with sudo ./scripts/cs2-host.sh update).");
            default:
                return (false, $"Unknown action: {type}.");
        }
    }
}
