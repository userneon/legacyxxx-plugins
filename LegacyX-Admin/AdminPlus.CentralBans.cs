using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
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
/// Central SteamID bans kept in the LEGACY-X database (issued from the Discord bot or the website).
/// Players are checked against the Root API when they connect and every few seconds while online,
/// so a ban issued elsewhere removes them from every server. Bans exist only in the database; this server
/// keeps no ban files. If the API can't be reached nobody is kicked (fail open).
/// </summary>
public partial class AdminPlus
{
    private CentralBanClient? _centralBanClient;
    private DateTime _centralBanLastErrorLogUtc = DateTime.MinValue;

    private void InitializeCentralBans()
    {
        var environment = LegacyXEnvironmentLoader.Load();
        if (!environment.GetModuleBoolean("ADMIN", "CENTRAL_BANS_ENABLED", true))
        {
            Console.WriteLine("[LEGACY-X Admin] Central bans are disabled: bans are neither enforced nor listed on this server.");
            return;
        }

        var apiBaseUrl = environment.GetModule("ADMIN", "API_BASE_URL").TrimEnd('/');
        var pluginSecret = environment.GetModule("ADMIN", "PLUGIN_SECRET", environment.Get("LEGACYX_PLUGIN_TOKEN"));
        var pluginId = environment.GetModule("ADMIN", "PLUGIN_ID", "legacyx-admin");
        if (string.IsNullOrWhiteSpace(apiBaseUrl) || string.IsNullOrWhiteSpace(pluginSecret))
        {
            LogError("Central bans are enabled but LEGACYX_ADMIN_API_BASE_URL or LEGACYX_ADMIN_PLUGIN_SECRET is missing.");
            return;
        }

        _centralBanClient = new CentralBanClient(apiBaseUrl, pluginId, pluginSecret);
        var sweepSeconds = environment.GetModuleInt("ADMIN", "CENTRAL_BANS_SWEEP_SECONDS", 30, 10, 300);
        AddTimer(sweepSeconds, SweepCentralBans, TimerFlags.REPEAT);
        _ = RefreshBanCacheAsync();
        Console.WriteLine($"[LEGACY-X Admin] Central bans enabled: checking on connect and every {sweepSeconds}s.");
    }

    /// <summary>Called on the main thread when a player is authorized.</summary>
    private void CheckCentralBan(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (_centralBanClient == null || player == null || !player.IsValid || player.IsBot) return;
        var ip = PlayerIp(player);
        _ = CheckAndKickAsync([player.SteamID.ToString()], ip.Length > 0 ? [ip] : []);
    }

    /// <summary>The player's IPv4 address without the port, or "" when it is not known.</summary>
    private static string PlayerIp(CCSPlayerController player)
    {
        var ip = (player.IpAddress ?? "").Split(':')[0];
        return System.Text.RegularExpressions.Regex.IsMatch(ip, @"^\d{1,3}(\.\d{1,3}){3}$") ? ip : "";
    }

    private void SweepCentralBans()
    {
        if (_centralBanClient == null) return;
        var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot && p.SteamID != 0).ToList();
        var steamIds = players.Select(p => p.SteamID.ToString()).Distinct().ToList();
        var ips = players.Select(PlayerIp).Where(ip => ip.Length > 0).Distinct().ToList();
        _ = RefreshBanCacheAsync();
        if (steamIds.Count == 0) return;
        _ = CheckAndKickAsync(steamIds, ips);
    }

    private async Task CheckAndKickAsync(List<string> steamIds, List<string> ips)
    {
        var client = _centralBanClient;
        if (client == null) return;

        var bans = new List<CentralBan>();
        var ipBans = new List<CentralIpBan>();
        try
        {
            // The API accepts up to 128 SteamIDs and 128 addresses per request.
            for (var start = 0; start < steamIds.Count; start += 128)
            {
                var result = await client.CheckAsync(steamIds.Skip(start).Take(128), ips.Skip(start).Take(128));
                bans.AddRange(result.Bans);
                ipBans.AddRange(result.IpBans);
            }
        }
        catch (Exception ex)
        {
            // Fail open, and keep the log readable while the API is down.
            if (DateTime.UtcNow - _centralBanLastErrorLogUtc > TimeSpan.FromMinutes(5))
            {
                _centralBanLastErrorLogUtc = DateTime.UtcNow;
                LogError($"Central ban check failed; nobody was kicked: {ex.Message}");
            }
            return;
        }
        if (bans.Count == 0 && ipBans.Count == 0) return;

        // Game state may only be touched on the main thread.
        Server.NextFrame(() =>
        {
            foreach (var ban in bans)
            {
                var target = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.SteamID.ToString() == ban.SteamId);
                if (target == null) continue;

                var name = SanitizeName(target.PlayerName);
                var term = ban.IsPermanent ? "permanent" : $"until {ban.ExpiresAt:yyyy-MM-dd HH:mm} UTC";
                target.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
                LogAction($"Central ban: removed {name} ({ban.SteamId}), {term}. Reason: {ban.Reason}");
                Console.WriteLine($"[LEGACY-X Admin] Central ban: removed {name} ({ban.SteamId}), {term}.");
            }
            foreach (var ipBan in ipBans)
            {
                foreach (var target in Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot && PlayerIp(p) == ipBan.Ip).ToList())
                {
                    target.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
                    LogAction($"Central IP ban: removed {SanitizeName(target.PlayerName)} ({target.SteamID}), {ipBan.Ip}. Reason: {ipBan.Reason}");
                    Console.WriteLine($"[LEGACY-X Admin] Central IP ban: removed {SanitizeName(target.PlayerName)} ({target.SteamID}).");
                }
            }
        });
    }
}

internal sealed class CentralBanClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly string _apiBaseUrl;
    private readonly string _pluginId;
    private readonly string _pluginSecret;

    internal CentralBanClient(string apiBaseUrl, string pluginId, string pluginSecret)
    {
        _apiBaseUrl = apiBaseUrl;
        _pluginId = pluginId;
        _pluginSecret = pluginSecret;
    }

    internal async Task<CentralBanResponse> CheckAsync(IEnumerable<string> steamIds, IEnumerable<string> ips)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _apiBaseUrl + "/api/v1/plugin/bans/check")
        {
            Content = JsonContent.Create(new { steamIds = steamIds.ToArray(), ips = ips.ToArray() }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _pluginSecret);
        request.Headers.Add("x-plugin-id", _pluginId);
        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"LEGACY-X API returned {(int)response.StatusCode}: {detail[..Math.Min(200, detail.Length)]}");
        }
        return await response.Content.ReadFromJsonAsync<CentralBanResponse>() ?? new CentralBanResponse();
    }

    /// <summary>Every active ban, SteamID and IP, for this server's ban menus.</summary>
    internal async Task<List<CentralListedBan>> ListActiveAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _apiBaseUrl + "/api/v1/plugin/bans/active");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _pluginSecret);
        request.Headers.Add("x-plugin-id", _pluginId);
        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"LEGACY-X API returned {(int)response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<CentralListResponse>();
        return body?.Bans ?? [];
    }
}

internal sealed class CentralBanResponse
{
    [JsonPropertyName("bans")] public List<CentralBan> Bans { get; set; } = [];
    [JsonPropertyName("ipBans")] public List<CentralIpBan> IpBans { get; set; } = [];
}

internal sealed class CentralIpBan
{
    [JsonPropertyName("ip")] public string Ip { get; set; } = string.Empty;
    [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
}

internal sealed class CentralListResponse
{
    [JsonPropertyName("bans")] public List<CentralListedBan> Bans { get; set; } = [];
}

internal sealed class CentralListedBan
{
    [JsonPropertyName("steamId")] public string? SteamId { get; set; }
    [JsonPropertyName("ip")] public string? Ip { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
    [JsonPropertyName("isPermanent")] public bool IsPermanent { get; set; }
    [JsonPropertyName("expiresAt")] public DateTimeOffset? ExpiresAt { get; set; }
}

internal sealed class CentralBan
{
    [JsonPropertyName("steamId")] public string SteamId { get; set; } = string.Empty;
    [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
    [JsonPropertyName("isPermanent")] public bool IsPermanent { get; set; }
    [JsonPropertyName("expiresAt")] public DateTimeOffset? ExpiresAt { get; set; }
}
