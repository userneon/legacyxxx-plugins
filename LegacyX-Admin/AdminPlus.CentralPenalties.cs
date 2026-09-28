using CounterStrikeSharp.API.Core;
using LegacyX.Shared.Configuration;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdminPlus;

/// <summary>
/// Game → API → database: every ban, unban, mute and gag issued on this server is also recorded in
/// the LEGACY-X database, so it shows on legacyx.cc/penalties, the player's profile and the Discord
/// penalties feed, and (bans) is enforced on every server. The local banned_user.cfg and
/// communication files keep working exactly as before; if the API is unreachable only the record is
/// missing. Uses LEGACYX_ADMIN_API_BASE_URL and LEGACYX_ADMIN_PLUGIN_SECRET, which then needs bans:write.
/// </summary>
public partial class AdminPlus
{
    private static readonly HttpClient PenaltyHttp = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly Regex SteamId64Pattern = new("^7656119\\d{10}$", RegexOptions.Compiled);
    private string _penaltyApiBaseUrl = "";
    private string _penaltyPluginId = "legacyx-admin";
    private string _penaltyPluginSecret = "";
    private bool _centralPenaltiesEnabled;

    private void InitializeCentralPenalties()
    {
        var environment = LegacyXEnvironmentLoader.Load();
        _centralPenaltiesEnabled = environment.GetModuleBoolean("ADMIN", "CENTRAL_PENALTIES_ENABLED", true);
        _penaltyApiBaseUrl = environment.GetModule("ADMIN", "API_BASE_URL").Trim().TrimEnd('/');
        _penaltyPluginId = environment.GetModule("ADMIN", "PLUGIN_ID", "legacyx-admin").Trim();
        _penaltyPluginSecret = environment.GetModule("ADMIN", "PLUGIN_SECRET", environment.Get("LEGACYX_PLUGIN_TOKEN")).Trim();
        if (!_centralPenaltiesEnabled)
        {
            Console.WriteLine("[LEGACY-X Admin] Penalty records are off: in-game bans, mutes and gags stay on this server only.");
            return;
        }
        if (_penaltyApiBaseUrl.Length == 0 || _penaltyPluginSecret.Length == 0 || _penaltyPluginSecret.StartsWith("REPLACE", StringComparison.Ordinal))
        {
            _centralPenaltiesEnabled = false;
            Console.WriteLine("[LEGACY-X Admin] Penalty records are off: LEGACYX_ADMIN_API_BASE_URL or LEGACYX_ADMIN_PLUGIN_SECRET is missing.");
            return;
        }
        Console.WriteLine("[LEGACY-X Admin] In-game bans, mutes and gags are recorded on the website.");
    }

    /// <summary>"Name (in-game)" as shown on the penalties page; the console is "Console".</summary>
    private static string PenaltyIssuerName(CCSPlayerController? caller)
    {
        var name = caller != null && caller.IsValid ? $"{caller.PlayerName} (in-game)" : "Console";
        return name.Length <= 64 ? name : name[..64];
    }

    private static string PenaltyReason(string? reason)
    {
        var text = (reason ?? "").Trim();
        if (text.Length == 0) text = "No reason";
        return text.Length <= 200 ? text : text[..200];
    }

    /// <summary>A ban on this server: recorded and enforced on every server. 0 minutes = permanent.</summary>
    internal void ReportBan(string steamId64, int minutes, string reason, CCSPlayerController? caller, string? issuerName = null)
    {
        if (!_centralPenaltiesEnabled || !SteamId64Pattern.IsMatch(steamId64)) return;
        var issuerSteamId = caller != null && caller.IsValid && SteamId64Pattern.IsMatch(caller.SteamID.ToString()) ? caller.SteamID.ToString() : null;
        Send("/api/v1/plugin/bans", new
        {
            steamId = steamId64,
            durationMinutes = Math.Clamp(minutes, 0, 525_600),
            reason = PenaltyReason(reason),
            issuerName = issuerName ?? PenaltyIssuerName(caller),
            source = "game",
            issuerSteamId,
        }, $"ban {steamId64}");
    }

    /// <summary>Lifts every central ban of the SteamID, including ones issued on the website or Discord.</summary>
    internal void ReportUnban(string steamId64, CCSPlayerController? caller)
    {
        if (!_centralPenaltiesEnabled || !SteamId64Pattern.IsMatch(steamId64)) return;
        Send("/api/v1/plugin/bans/revoke", new { steamId = steamId64, issuerName = PenaltyIssuerName(caller), reason = "Lifted in-game" }, $"unban {steamId64}");
    }

    /// <summary>type is AdminPlus's "MUTE" (voice) or "GAG" (chat).</summary>
    internal void ReportCommunication(ulong steamId, string type, int minutes, string reason, CCSPlayerController? caller)
    {
        var id = steamId.ToString();
        var apiType = CommunicationApiType(type);
        if (!_centralPenaltiesEnabled || apiType == null || !SteamId64Pattern.IsMatch(id)) return;
        Send("/api/v1/plugin/penalties", new
        {
            steamId = id,
            type = apiType,
            durationMinutes = Math.Clamp(minutes, 0, 525_600),
            reason = PenaltyReason(reason),
            issuerName = PenaltyIssuerName(caller),
        }, $"{apiType} {id}");
    }

    internal void ReportCommunicationLifted(ulong steamId, string type, CCSPlayerController? caller)
    {
        var id = steamId.ToString();
        var apiType = CommunicationApiType(type);
        if (!_centralPenaltiesEnabled || apiType == null || !SteamId64Pattern.IsMatch(id)) return;
        Send("/api/v1/plugin/penalties/revoke", new { steamId = id, type = apiType, issuerName = PenaltyIssuerName(caller) }, $"un{apiType} {id}");
    }

    /// <summary>
    /// !cleanbans by an owner: every ban on the website and every server is lifted. The API checks
    /// the SteamID is a website OWNER itself; the owner is told how many were lifted.
    /// </summary>
    internal void ReportAllBansCleared(CCSPlayerController owner)
    {
        if (!_centralPenaltiesEnabled || !owner.IsValid || !SteamId64Pattern.IsMatch(owner.SteamID.ToString())) return;
        var url = _penaltyApiBaseUrl + "/api/v1/plugin/bans/revoke-all";
        var pluginId = _penaltyPluginId;
        var secret = _penaltyPluginSecret;
        var body = new { issuerSteamId = owner.SteamID.ToString(), issuerName = PenaltyIssuerName(owner) };
        var slot = owner.Slot;
        _ = Task.Run(async () =>
        {
            string message;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                request.Headers.Add("x-plugin-id", pluginId);
                using var response = await PenaltyHttp.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<RevokeAllResult>();
                    message = $"Website: {result?.BansLifted ?? 0} ban lifted on every server.";
                }
                else
                {
                    message = (int)response.StatusCode == 403 ? "Website: only an owner can clear the website bans." : $"Website bans were not cleared (HTTP {(int)response.StatusCode}).";
                }
            }
            catch (Exception exception)
            {
                message = $"Website bans were not cleared ({exception.GetType().Name}).";
            }
            Console.WriteLine($"[LEGACY-X Admin] !cleanbans: {message}");
            CounterStrikeSharp.API.Server.NextFrame(() =>
            {
                var player = CounterStrikeSharp.API.Utilities.GetPlayerFromSlot(slot);
                if (player != null && player.IsValid) player.PrintToChat($" {message}");
            });
        });
    }

    private sealed record RevokeAllResult(int BansLifted, int PenaltiesLifted);

    internal static string? CommunicationApiType(string type) => type switch
    {
        "MUTE" => "comm",
        "GAG" => "gag",
        _ => null,
    };

    /// <summary>Fire and forget, off the game thread. The token is never logged.</summary>
    private void Send(string path, object body, string what)
    {
        var url = _penaltyApiBaseUrl + path;
        var pluginId = _penaltyPluginId;
        var secret = _penaltyPluginSecret;
        _ = Task.Run(async () =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                request.Headers.Add("x-plugin-id", pluginId);
                using var response = await PenaltyHttp.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    Console.WriteLine($"[LEGACY-X Admin] Recording {what} on the website failed: HTTP {(int)response.StatusCode}{((int)response.StatusCode is 401 or 403 ? " (token needs bans:write)" : "")}.");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[LEGACY-X Admin] Recording {what} on the website failed: {exception.GetType().Name}.");
            }
        });
    }
}
