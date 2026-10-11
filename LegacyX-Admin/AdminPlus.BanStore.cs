using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using LegacyX.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AdminPlus;

/// <summary>
/// Bans live only in the LEGACY-X database. This server keeps no ban files: SteamBans and IpBans are an in-memory copy
/// of the database's active bans (refreshed every few seconds and after each change), used for the ban menus,
/// !baninfo and as a head start when somebody is banned a moment before the API answers. A ban is enforced from the
/// database itself (see CentralBans), so a restart loses nothing.
/// </summary>
public partial class AdminPlus
{
    private DateTime _banCacheErrorLogUtc = DateTime.MinValue;

    /// <summary>The line the ban menus and !baninfo show: <c>banid "id" "name" ip:x expiry:n // reason</c>.</summary>
    private static string SteamBanLine(string steamId, string nick, string ip, long expiry, string reason) =>
        $"banid \"{steamId}\" \"{nick}\" ip:{(string.IsNullOrEmpty(ip) ? "-" : ip)} expiry:{expiry} // {reason}";

    private static string IpBanLine(string ip, long expiry, string reason) => $"addip \"{ip}\" expiry:{expiry} // {reason}";

    private static long ExpiryUnix(DateTimeOffset? expiresAt) => expiresAt?.ToUnixTimeSeconds() ?? 0;

    /// <summary>Remembers a ban made on this server until the next refresh brings the database's copy.</summary>
    private void RememberSteamBan(string steamId, long expiry, string nick, string ip, string reason)
    {
        lock (_lock) SteamBans[steamId] = (expiry, SteamBanLine(steamId, nick, ip, expiry, SanitizeForCfg(reason)), nick, ip);
    }

    private void RememberIpBan(string ip, long expiry, string nick, string reason)
    {
        lock (_lock) IpBans[ip] = (expiry, IpBanLine(ip, expiry, SanitizeForCfg(reason)), nick);
    }

    /// <summary>Fills SteamBans and IpBans from GET /plugin/bans/active. On any failure the current copy stays.</summary>
    internal async Task RefreshBanCacheAsync()
    {
        var client = _centralBanClient;
        if (client == null) return;
        try
        {
            var bans = await client.ListActiveAsync();
            lock (_lock)
            {
                SteamBans.Clear();
                IpBans.Clear();
                foreach (var ban in bans)
                {
                    var expiry = ban.IsPermanent ? 0 : Math.Max(1, ExpiryUnix(ban.ExpiresAt));
                    var reason = SanitizeForCfg(ban.Reason);
                    if (!string.IsNullOrEmpty(ban.SteamId))
                    {
                        var nick = string.IsNullOrWhiteSpace(ban.Name) ? "Unknown" : SanitizeName(ban.Name);
                        SteamBans[ban.SteamId] = (expiry, SteamBanLine(ban.SteamId, nick, "-", expiry, reason), nick, "-");
                    }
                    else if (!string.IsNullOrEmpty(ban.Ip))
                    {
                        IpBans[ban.Ip] = (expiry, IpBanLine(ban.Ip, expiry, reason), ban.Ip);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            if (DateTime.UtcNow - _banCacheErrorLogUtc > TimeSpan.FromMinutes(5))
            {
                _banCacheErrorLogUtc = DateTime.UtcNow;
                LogError($"Ban list refresh failed; the last copy stays: {exception.Message}");
            }
        }
    }

    /// <summary>An IP ban: recorded in the database and enforced on every server. 0 minutes = permanent.</summary>
    internal void ReportIpBan(string ip, int minutes, string reason, CCSPlayerController? caller)
    {
        if (!_centralPenaltiesEnabled) return;
        var issuerSteamId = caller != null && caller.IsValid && SteamId64Pattern.IsMatch(caller.SteamID.ToString()) ? caller.SteamID.ToString() : null;
        Send("/api/v1/plugin/bans/ip", new
        {
            ip,
            durationMinutes = Math.Clamp(minutes, 0, 525_600),
            reason = PenaltyReason(reason),
            issuerName = PenaltyIssuerName(caller),
            issuerSteamId,
        }, $"ip ban {ip}");
    }

    /// <summary>Lifts the IP ban; <paramref name="done"/> gets how many bans were lifted (null when the API did not answer).</summary>
    internal void ReportIpUnban(string ip, CCSPlayerController? caller, Action<int?> done)
    {
        var url = _penaltyApiBaseUrl + "/api/v1/plugin/bans/ip/revoke";
        var pluginId = _penaltyPluginId;
        var secret = _penaltyPluginSecret;
        var body = new { ip, issuerName = PenaltyIssuerName(caller) };
        _ = Task.Run(async () => done(await PostLiftAsync(url, pluginId, secret, body, "ip unban " + ip)));
    }

    /// <summary>Lifts every ban of the SteamID; <paramref name="done"/> gets how many were lifted (null when the API did not answer).</summary>
    internal void ReportUnbanWithResult(string steamId64, CCSPlayerController? caller, Action<int?> done)
    {
        var url = _penaltyApiBaseUrl + "/api/v1/plugin/bans/revoke";
        var pluginId = _penaltyPluginId;
        var secret = _penaltyPluginSecret;
        var body = new { steamId = steamId64, issuerName = PenaltyIssuerName(caller), reason = "Lifted in-game" };
        _ = Task.Run(async () => done(await PostLiftAsync(url, pluginId, secret, body, "unban " + steamId64)));
    }

    private static async Task<int?> PostLiftAsync(string url, string pluginId, string secret, object body, string what)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
            request.Headers.Add("x-plugin-id", pluginId);
            using var response = await PenaltyHttp.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[LEGACY-X Admin] Recording {what} on the website failed: HTTP {(int)response.StatusCode}{((int)response.StatusCode is 401 or 403 ? " (token needs bans:write)" : "")}.");
                return null;
            }
            var result = await response.Content.ReadFromJsonAsync<LiftResult>();
            return (result?.BansLifted ?? 0) + (result?.PenaltiesLifted ?? 0);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[LEGACY-X Admin] Recording {what} on the website failed: {exception.GetType().Name}.");
            return null;
        }
    }

    private sealed class LiftResult
    {
        [JsonPropertyName("bansLifted")] public int BansLifted { get; set; }
        [JsonPropertyName("penaltiesLifted")] public int PenaltiesLifted { get; set; }
    }
}
