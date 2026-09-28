using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using LegacyX.Admin.Authorization;
using LegacyX.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdminPlus;

/// <summary>
/// In-game staff permissions come only from the LEGACY-X API (website/database). Every connecting
/// player starts with no admin data; the plugin asks the API about them and grants the
/// CounterStrikeSharp flags of their role for this server (Authorization/StaffPermissions.cs).
/// Online players are re-checked every AUTH_REFRESH_SECONDS, so a revoke or role change on the
/// website reaches the server within one refresh, and a timed assignment is dropped the moment it
/// expires. Fail closed: no configuration, an unreachable API or an invalid answer never grants
/// anything, and a grant that could not be re-confirmed for AUTH_CACHE_SECONDS is removed.
/// </summary>
public partial class AdminPlus
{
    private const string AuthLogPrefix = "[LegacyX.Admin]";
    private static readonly HttpClient AuthorizationHttp = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    private AdminAuthorizationClient? _authorizationClient;
    private AuthorizationStore _authorizationStore = new(TimeSpan.FromSeconds(180));
    private bool _authorizationActive;
    private DateTime _authorizationLastErrorLogUtc = DateTime.MinValue;

    private static void AuthLog(string message) => Console.WriteLine($"{AuthLogPrefix} {message}");

    private void InitializeStaffAuthorization()
    {
        _authorizationActive = true;
        var environment = LegacyXEnvironmentLoader.Load();
        var apiBaseUrl = environment.GetModule("ADMIN", "API_BASE_URL").Trim();
        var pluginId = environment.GetModule("ADMIN", "PLUGIN_ID", "legacyx-admin").Trim();
        var pluginToken = environment.GetModule("ADMIN", "PLUGIN_SECRET", environment.Get("LEGACYX_PLUGIN_TOKEN")).Trim();
        var serverId = LegacyXServerRuntime.Load(environment).ServerId;
        var refreshSeconds = environment.GetModuleInt("ADMIN", "AUTH_REFRESH_SECONDS", 60, 15, 600);
        var cacheSeconds = environment.GetModuleInt("ADMIN", "AUTH_CACHE_SECONDS", 180, 30, 3600);
        if (cacheSeconds <= refreshSeconds)
        {
            AuthLog($"LEGACYX_ADMIN_AUTH_CACHE_SECONDS ({cacheSeconds}) must exceed AUTH_REFRESH_SECONDS ({refreshSeconds}); using {refreshSeconds * 3}.");
            cacheSeconds = refreshSeconds * 3;
        }
        _authorizationStore = new AuthorizationStore(TimeSpan.FromSeconds(cacheSeconds));

        var missing = new List<string>();
        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri) || (apiUri.Scheme != Uri.UriSchemeHttps && !apiUri.IsLoopback)) missing.Add("LEGACYX_ADMIN_API_BASE_URL (https)");
        if (pluginToken.Length < 24) missing.Add("LEGACYX_ADMIN_PLUGIN_SECRET");
        if (string.IsNullOrWhiteSpace(pluginId)) missing.Add("LEGACYX_ADMIN_PLUGIN_ID");
        if (!System.Text.RegularExpressions.Regex.IsMatch(serverId, "^[A-Za-z0-9._:-]{1,64}$")) missing.Add("LEGACYX_SERVER_ID");
        if (missing.Count > 0)
        {
            AuthLog($"Staff authorization is not configured ({string.Join(", ", missing)}). Nobody receives admin permissions.");
            _authorizationClient = null;
        }
        else
        {
            _authorizationClient = new AdminAuthorizationClient(AuthorizationHttp, apiBaseUrl, pluginId, pluginToken, serverId, TimeSpan.FromSeconds(5));
            AuthLog($"Staff authorization from the LEGACY-X API for server '{serverId}': refresh every {refreshSeconds}s, grants kept at most {cacheSeconds}s without confirmation.");
        }

        WarnAboutLocalAdminFile();
        AddTimer(refreshSeconds, () => _ = RefreshOnlineStaffAsync(), TimerFlags.REPEAT);
        AddTimer(5.0f, RemoveExpiredStaff, TimerFlags.REPEAT);
    }

    /// <summary>Hot reload / map start: strip whatever admin data online players carry and ask the API again.</summary>
    private void AuthorizeOnlinePlayers()
    {
        if (!_authorizationActive) return;
        foreach (var player in OnlineHumans()) SyncPlayerPermissions(player, null);
        _ = RefreshOnlineStaffAsync();
    }

    /// <summary>Main thread, from OnClientAuthorized.</summary>
    private void OnStaffPlayerAuthorized(int slot, SteamID steamId)
    {
        if (!_authorizationActive) return;
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null || !player.IsValid || player.IsBot) return;
        var id = steamId.SteamId64;
        // Whatever CounterStrikeSharp loaded for this SteamID (e.g. a leftover admins.json) does not count.
        SyncPlayerPermissions(player, null);
        _ = AuthorizeAsync(new[] { id }, "connect");
    }

    /// <summary>
    /// Main thread, from player_connect_full. A map change reconnects every player without a new
    /// OnClientAuthorized, after the disconnect cleared their permissions: re-apply a still-valid grant
    /// at once, otherwise ask the API.
    /// </summary>
    private void OnStaffPlayerConnectFull(CCSPlayerController player)
    {
        if (!_authorizationActive || player == null || !player.IsValid || player.IsBot || player.SteamID == 0) return;
        var grant = _authorizationStore.Get(player.SteamID, DateTimeOffset.UtcNow);
        if (grant != null) SyncPlayerPermissions(player, grant);
        else _ = AuthorizeAsync(new[] { player.SteamID }, "connect");
    }

    private void OnStaffPlayerDisconnected(int slot)
    {
        if (!_authorizationActive) return;
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null || !player.IsValid || player.IsBot || player.SteamID == 0) return;
        var steamId = player.SteamID;
        if (_authorizationStore.Forget(steamId)) AuthLog($"{steamId} left; staff permissions cleared.");
        RemoveStaffData(steamId);
    }

    private Task RefreshOnlineStaffAsync()
    {
        var ids = OnlineHumans().Select(player => player.SteamID).Where(id => id != 0).Distinct().ToList();
        return ids.Count == 0 ? Task.CompletedTask : AuthorizeAsync(ids, "refresh");
    }

    private async Task AuthorizeAsync(IReadOnlyCollection<ulong> steamIds, string reason)
    {
        var store = _authorizationStore;
        var ticket = store.BeginRequest(steamIds);
        var client = _authorizationClient;
        var result = client == null
            ? AuthorizationResult.Failed("not configured")
            : await client.AuthorizeAsync(steamIds).ConfigureAwait(false);

        if (!result.Success && client != null && DateTime.UtcNow - _authorizationLastErrorLogUtc > TimeSpan.FromMinutes(1))
        {
            _authorizationLastErrorLogUtc = DateTime.UtcNow;
            AuthLog($"Staff lookup ({reason}, {steamIds.Count} player(s)) failed: {result.Error}. No new permissions are granted.");
        }

        Server.NextFrame(() =>
        {
            if (!ReferenceEquals(store, _authorizationStore)) return;
            var now = DateTimeOffset.UtcNow;
            foreach (var steamId in steamIds)
            {
                AuthorizationChange change;
                if (result.Success)
                {
                    result.Players.TryGetValue(steamId, out var answer);
                    change = store.Apply(steamId, ticket, answer is { IsStaff: true } ? answer : null, now);
                }
                else
                {
                    change = store.ApplyFailure(steamId, ticket, now);
                }
                if (change == AuthorizationChange.Stale) continue;

                var player = FindOnlineHuman(steamId);
                var grant = store.Get(steamId, now);
                if (player != null) SyncPlayerPermissions(player, grant);
                else if (grant == null) RemoveStaffData(steamId);
                LogAuthorizationChange(steamId, change, grant, player);
            }
        });
    }

    private void RemoveExpiredStaff()
    {
        if (!_authorizationActive) return;
        foreach (var steamId in _authorizationStore.RemoveExpired(DateTimeOffset.UtcNow))
        {
            var player = FindOnlineHuman(steamId);
            if (player != null) SyncPlayerPermissions(player, null);
            else RemoveStaffData(steamId);
            AuthLog($"{steamId} staff permissions removed: the assignment expired or could not be re-confirmed.");
        }
    }

    /// <summary>Makes the player's CounterStrikeSharp admin data exactly match <paramref name="grant"/> (null = none).</summary>
    private void SyncPlayerPermissions(CCSPlayerController player, PlayerAuthorization? grant)
    {
        if (player == null || !player.IsValid || player.IsBot || player.SteamID == 0) return;
        RemoveStaffData(player.SteamID);
        if (grant == null || !grant.IsStaff) return;

        var role = StaffPermissions.For(grant.Role);
        // Keyed by SteamID, not the controller: right after a connect or map change the controller's
        // AuthorizedSteamID can still be empty, and the controller-based calls then silently do nothing.
        var steamId = new SteamID(player.SteamID);
        AdminManager.AddPlayerPermissions(steamId, role.Flags.ToArray());
        AdminManager.SetPlayerImmunity(steamId, role.Immunity);
        adminImmunity[player.SteamID] = (int)role.Immunity;
        adminStamina[player.SteamID] = role.Stamina;
        adminStaffRoles[player.SteamID] = role.Name;
    }

    private void RemoveStaffData(ulong steamId)
    {
        AdminManager.RemovePlayerAdminData(new SteamID(steamId));
        adminImmunity.Remove(steamId);
        adminStamina.Remove(steamId);
        adminStaffRoles.Remove(steamId);
    }

    private void ClearAllStaffPermissions()
    {
        if (!_authorizationActive) return;
        foreach (var steamId in _authorizationStore.Clear()) RemoveStaffData(steamId);
        _authorizationActive = false;
    }

    private void LogAuthorizationChange(ulong steamId, AuthorizationChange change, PlayerAuthorization? grant, CCSPlayerController? player)
    {
        var who = player != null ? $"{SanitizeName(player.PlayerName)} ({steamId})" : steamId.ToString();
        var role = grant != null ? StaffPermissions.For(grant.Role).Name : "PLAYER";
        var expiry = grant?.ExpiresAt is { } expiresAt ? $" until {expiresAt:yyyy-MM-dd HH:mm} UTC" : string.Empty;
        switch (change)
        {
            case AuthorizationChange.Granted: AuthLog($"{who} authorized as {role}{expiry}."); break;
            case AuthorizationChange.Updated: AuthLog($"{who} role changed to {role}{expiry}."); break;
            case AuthorizationChange.Revoked: AuthLog($"{who} staff permissions revoked."); break;
        }
    }

    private IReadOnlyList<PlayerAuthorization> OnlineStaff() => _authorizationStore.Snapshot(DateTimeOffset.UtcNow);

    private static IEnumerable<CCSPlayerController> OnlineHumans() =>
        Utilities.GetPlayers().Where(player => player != null && player.IsValid && !player.IsBot && !player.IsHLTV && player.SteamID != 0);

    private static CCSPlayerController? FindOnlineHuman(ulong steamId) => OnlineHumans().FirstOrDefault(player => player.SteamID == steamId);

    /// <summary>admins.json is no longer a source of staff; say so if the host still has entries in it.</summary>
    private static void WarnAboutLocalAdminFile()
    {
        try
        {
            var path = Path.Combine(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "configs", "admins.json");
            if (!File.Exists(path)) return;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.EnumerateObject().Any())
                AuthLog($"{path} still lists admins. They are ignored: every connecting player's admin data is replaced by the LEGACY-X API answer. Empty the file to avoid confusion.");
        }
        catch (Exception ex)
        {
            AuthLog($"Could not inspect admins.json: {ex.GetType().Name}");
        }
    }
}
