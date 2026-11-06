using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using LegacyX.Shared.Configuration;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdminPlus;

/// <summary>
/// css_cs2ac_flag &lt;steamid64&gt; &lt;detection...&gt; (server console only; what CS2AC's punishment command runs, see scripts/cs2-host.sh cs2ac): the detection is sent to the
/// website first (POST /plugin/anticheat/flags), which stores it as evidence and answers "ban" or "flag" by the rules in the API's anticheat.ts (never staff, warmup, Fun
/// servers, our own cheat toys, a detector firing on many players at once; heavy detectors at once, medium ones on a repeat). Only "ban" runs css_ban, with the ban screen
/// and the red chat line. A flag is shown to the staff online and written to the console. When the website cannot be reached the same guards run here and only a heavy
/// detector bans (the owner trusts CS2AC), so a down API neither bans everybody nor lets a cheater stay.
/// </summary>
public partial class AdminPlus
{
    private static readonly string[] HeavyDetectors = { "aimbot", "aimlock", "silentaim", "silent aim", "antiaim", "anti-aim", "anti aim", "doubletap", "double tap" };

    private void CmdAntiCheatFlag(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null || info.ArgCount < 3) return;
        var steamId = info.GetArg(1);
        if (!SteamId64Pattern.IsMatch(steamId)) return;
        // The detector's name goes into a ban reason and a console line: plain characters only.
        var detector = new string(string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(info.GetArg)).Where(ch => char.IsLetterOrDigit(ch) || " .,'+-:()".Contains(ch)).ToArray()).Trim();
        if (detector.Length == 0) return;
        if (detector.Length > 48) detector = detector[..48];

        var online = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.SteamID.ToString() == steamId);
        var name = online != null ? SanitizeName(online.PlayerName) : steamId;
        var context = new
        {
            warmup = InWarmup(),
            funServer = IsFunServer(),
            gameMode = gameMode.Length > 0,
            cheatToys = online != null && (wallhackSlots.Contains(online.Slot) || aimlockSlots.Contains(online.Slot) || silentAimSlots.Contains(online.Slot) || bhopSlots.Contains(online.Slot)),
        };
        var serverId = LegacyXServerRuntime.Load(LegacyXEnvironmentLoader.Load()).ServerId;
        var map = Server.MapName ?? "";
        var payload = new { steamId, detector, serverId = string.IsNullOrWhiteSpace(serverId) ? null : serverId, map, mode = LegacyXServerRuntime.Load(LegacyXEnvironmentLoader.Load()).ServerMode, context };

        _ = Task.Run(async () =>
        {
            (string Action, string Reason, string Why)? answer = null;
            if (_centralPenaltiesEnabled)
            {
                try { answer = await PostAntiCheatFlagAsync(payload); }
                catch (Exception exception) { Console.WriteLine($"[LEGACY-X Admin] Anti-cheat: the website could not be asked ({exception.Message}); deciding here."); }
            }
            var decided = answer ?? LocalAntiCheatDecision(detector, context.warmup, context.funServer, context.gameMode, context.cheatToys);
            Server.NextFrame(() => ApplyAntiCheatDecision(steamId, name, detector, decided.Action, decided.Reason, decided.Why));
        });
    }

    private async Task<(string Action, string Reason, string Why)> PostAntiCheatFlagAsync(object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _penaltyApiBaseUrl + "/api/v1/plugin/anticheat/flags") { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _penaltyPluginSecret);
        request.Headers.Add("x-plugin-id", _penaltyPluginId);
        using var response = await PenaltyHttp.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        var action = root.TryGetProperty("action", out var a) ? a.GetString() ?? "flag" : "flag";
        var reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
        var why = root.TryGetProperty("decision", out var d) ? d.GetString() ?? "" : "";
        return (action == "ban" ? "ban" : "flag", reason, why);
    }

    /// <summary>The guards of the website's rules that need no history: the fallback when the website cannot be asked.</summary>
    private static (string Action, string Reason, string Why) LocalAntiCheatDecision(string detector, bool warmup, bool fun, bool mode, bool toys)
    {
        var reason = $"CS2AC: {detector}";
        if (toys || mode || fun || warmup) return ("flag", reason, "Local guard: warmup, a Fun server, a match mode or a cheat toy.");
        var lower = detector.ToLowerInvariant();
        return HeavyDetectors.Any(lower.Contains) ? ("ban", reason, "Website unreachable: a heavy detector bans.") : ("flag", reason, "Website unreachable: only heavy detectors ban.");
    }

    private void ApplyAntiCheatDecision(string steamId, string name, string detector, string action, string reason, string why)
    {
        LogAction($"Anti-cheat {action}: {name} ({steamId}) {detector}. {why}");
        if (action == "ban")
        {
            var safe = new string((string.IsNullOrWhiteSpace(reason) ? $"CS2AC: {detector}" : reason).Where(ch => char.IsLetterOrDigit(ch) || " .,'+-:()".Contains(ch)).ToArray()).Trim();
            Server.ExecuteCommand($"css_ban {steamId} 0 {safe}");
            return;
        }
        // A flag is for the staff on this server, not for everybody (it would only tip a cheater off).
        foreach (var staff in Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot && AdminManager.PlayerHasPermissions(p, "@css/ban")))
            staff.PrintToChat(LegacyXChat.System($"{{white}}Anti-cheat{{grey}} flagged {{white}}{EscapeForStringFormat(name)}{{grey}}: {{white}}{EscapeForStringFormat(detector)}{{grey}}. Not banned: {EscapeForStringFormat(why)}"));
    }
}
