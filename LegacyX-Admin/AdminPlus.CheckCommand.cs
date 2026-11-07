using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using LegacyX.Shared.Configuration;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace AdminPlus;

/// <summary>
/// <c>!check &lt;name&gt;</c> in game (owner request 2026-11-07): a staff member asks a player to run the LEGACY-X Checker without leaving the match.
/// The website makes the one-time code (the staff member must be Admin, Manager or Owner there; the server cannot make one by itself), and the link
/// and code are shown to the asked player alone, in their chat and console. The staff member only hears that it was sent, never the code.
/// </summary>
public partial class AdminPlus
{
    private void RegisterCheckCommand() => AddPermCmd("@css/ban", "css_check", "<#userid|name>", CmdCheck);

    private sealed record CheckAnswer(string? Code, string? ExpiresAt, string? Link);

    private void CmdCheck(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid)
        {
            Server.PrintToConsole("!check is used in game, by a staff member.");
            return;
        }
        if (info.ArgCount < 2)
        {
            caller.PrintToChat(LegacyXChat.System("Use {white}!check <name>{grey}."));
            return;
        }
        if (!ResolveTargets(caller, info.GetArg(1), false, false, false, out var targets, out _, out var label, out var scope) || scope != TargetScope.Single || targets.Count != 1)
            return;
        var target = targets[0];
        if (target.IsBot || target.SteamID == caller.SteamID)
        {
            caller.PrintToChat(LegacyXChat.System("Pick another player, not a bot and not yourself."));
            return;
        }
        var issuerId = caller.SteamID.ToString();
        var targetId = target.SteamID.ToString();
        if (!_centralPenaltiesEnabled || !SteamId64Pattern.IsMatch(issuerId) || !SteamId64Pattern.IsMatch(targetId))
        {
            caller.PrintToChat(LegacyXChat.System("The website is not connected on this server, so a check cannot be asked for."));
            return;
        }
        var callerSlot = caller.Slot;
        var targetSlot = target.Slot;
        var url = _penaltyApiBaseUrl + "/api/v1/plugin/checks";
        var pluginId = _penaltyPluginId;
        var secret = _penaltyPluginSecret;
        caller.PrintToChat(LegacyXChat.System($"Asking {label} for a check ..."));
        _ = Task.Run(async () =>
        {
            CheckAnswer? answer = null;
            string failure = "The check was not made. Try again.";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { issuerSteamId = issuerId, targetSteamId = targetId }) };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                request.Headers.Add("x-plugin-id", pluginId);
                using var response = await PenaltyHttp.SendAsync(request);
                var status = (int)response.StatusCode;
                if (response.IsSuccessStatusCode) answer = await response.Content.ReadFromJsonAsync<CheckAnswer>(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                else if (status == 403) failure = "Only an Admin, Manager or Owner signed in on the website can ask for a check.";
                else if (status == 409) failure = $"{label} already has an open check.";
                else Console.WriteLine($"[LEGACY-X Admin] !check failed: HTTP {status}{(status is 401 ? " (token needs bans:write)" : "")}.");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[LEGACY-X Admin] !check failed: {exception.GetType().Name}.");
            }
            Server.NextFrame(() =>
            {
                var asker = Utilities.GetPlayerFromSlot(callerSlot);
                var asked = Utilities.GetPlayerFromSlot(targetSlot);
                if (answer?.Code == null)
                {
                    if (asker != null && asker.IsValid) asker.PrintToChat(LegacyXChat.System(failure));
                    return;
                }
                // Only the asked player sees the link and the code (and only if they are still the same person).
                if (asked == null || !asked.IsValid || asked.SteamID.ToString() != targetId)
                {
                    if (asker != null && asker.IsValid) asker.PrintToChat(LegacyXChat.System($"{label} left before the check could be shown. The code is on the website's Checks page."));
                    return;
                }
                asked.PrintToChat(LegacyXChat.System("Staff asked you to run the {white}LEGACY-X Checker{grey}. It only reads this PC and sends a short result; nothing is changed."));
                if (!string.IsNullOrEmpty(answer.Link)) asked.PrintToChat(LegacyXChat.System($"Download: {{white}}{answer.Link}"));
                asked.PrintToChat(LegacyXChat.System($"Your code: {{white}}{answer.Code}"));
                asked.PrintToConsole($"[LEGACY-X Checker] Download: {answer.Link}");
                asked.PrintToConsole($"[LEGACY-X Checker] Code: {answer.Code}");
                if (asker != null && asker.IsValid) asker.PrintToChat(LegacyXChat.System($"The check link and code were shown to {label} only."));
            });
        });
    }
}
