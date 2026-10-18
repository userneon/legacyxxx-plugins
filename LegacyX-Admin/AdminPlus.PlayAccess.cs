using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using LegacyX.Shared.Configuration;
using System;
using System.Threading.Tasks;

namespace AdminPlus;

/// <summary>
/// Who may stay on the server (owner request 2026-10-18). A player who connects without being signed in on legacyx.cc, or while
/// blocked for leaving ranked matches too often (3rd leave in 24 hours: 30 minutes, then 30 more each time), is told why in
/// the chat and removed a few seconds later. Never a bare "disconnected": the message says what to do. Staff are never touched, and
/// if the API cannot be reached nobody is removed (fail open). Switch each part off with LEGACYX_ADMIN_REQUIRE_LOGIN=false and
/// LEGACYX_ADMIN_ENFORCE_LEAVE_COOLDOWN=false (a Fun server may not want the cooldown).
/// </summary>
public partial class AdminPlus
{
    private bool _requireLogin;
    private bool _enforceLeaveCooldown;
    private DateTime _playAccessLastErrorLogUtc = DateTime.MinValue;
    /// <summary>Seconds the player has to read the message before they are removed.</summary>
    private const float PlayAccessReadSeconds = 10f;

    private void InitializePlayAccess(LegacyXEnvironment environment)
    {
        _requireLogin = environment.GetModuleBoolean("ADMIN", "REQUIRE_LOGIN", true);
        _enforceLeaveCooldown = environment.GetModuleBoolean("ADMIN", "ENFORCE_LEAVE_COOLDOWN", true);
        Console.WriteLine($"[LEGACY-X Admin] Players must be signed in on the website: {(_requireLogin ? "yes" : "no")}; leave cooldown enforced: {(_enforceLeaveCooldown ? "yes" : "no")}.");
    }

    /// <summary>Called on the main thread when a player is authorized.</summary>
    private void CheckPlayAccess(int slot)
    {
        var client = _centralBanClient;
        var player = Utilities.GetPlayerFromSlot(slot);
        if (client == null || (!_requireLogin && !_enforceLeaveCooldown) || player == null || !player.IsValid || player.IsBot || player.IsHLTV || player.SteamID == 0) return;
        var steamId = player.SteamID.ToString();
        // Staff may always join (they test things before they have signed in anywhere).
        if (AdminManager.PlayerHasPermissions(new SteamID(player.SteamID), "@css/generic")) return;
        _ = Task.Run(async () =>
        {
            PlayAccessEntry? access;
            try
            {
                access = await client.AccessAsync(steamId);
            }
            catch (Exception ex)
            {
                // Fail open, and keep the log readable while the API is down.
                if (DateTime.UtcNow - _playAccessLastErrorLogUtc > TimeSpan.FromMinutes(5))
                {
                    _playAccessLastErrorLogUtc = DateTime.UtcNow;
                    LogError($"Play access check failed; nobody was removed: {ex.Message}");
                }
                return;
            }
            if (access == null) return;
            string[]? lines = null;
            string reason = "";
            if (_enforceLeaveCooldown && access.Cooldown is { } cooldown && cooldown.Until > DateTimeOffset.UtcNow)
            {
                var minutes = Math.Max(1, (int)Math.Ceiling((cooldown.Until - DateTimeOffset.UtcNow).TotalMinutes));
                reason = $"leave cooldown, {minutes} min left";
                lines =
                [
                    $"You left matches too often. You can play again in {{white}}{minutes} minute{(minutes == 1 ? "" : "s")}{{grey}} (at {cooldown.Until:HH:mm} UTC).",
                    "Leaving a match costs EXP; the 3rd time in 24 hours also blocks you for 30 minutes, and 30 more each time after that.",
                ];
            }
            else if (_requireLogin && !access.LoggedIn)
            {
                reason = "not signed in on the website";
                lines =
                [
                    "You are not signed in. Sign in with Steam at {white}legacyx.cc{grey}, then press {white}Connect{grey} on the website.",
                    "Your LEGACY-X account is your Steam account: it keeps your rank, skins and stats.",
                ];
            }
            if (lines == null) return;
            Server.NextFrame(() => NoticeAndRemove(slot, steamId, lines, reason));
        });
    }

    /// <summary>Tells the player why (chat and centre of the screen), then removes them after a short while.</summary>
    private void NoticeAndRemove(int slot, string steamId, string[] lines, string reason)
    {
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null || !player.IsValid || player.SteamID.ToString() != steamId) return;
        void Say()
        {
            var current = Utilities.GetPlayerFromSlot(slot);
            if (current == null || !current.IsValid || current.SteamID.ToString() != steamId) return;
            foreach (var line in lines) current.PrintToChat(LegacyXChat.System(line));
            current.PrintToChat(LegacyXChat.System($"You will be removed from this server in {PlayAccessReadSeconds:0} seconds."));
        }
        // The first message may be lost while they are still loading in: say it again a little later, then remove.
        Say();
        AddTimer(5f, Say, TimerFlags.STOP_ON_MAPCHANGE);
        AddTimer(PlayAccessReadSeconds, () =>
        {
            var current = Utilities.GetPlayerFromSlot(slot);
            if (current == null || !current.IsValid || current.SteamID.ToString() != steamId) return;
            // Staff rights may only have arrived after the first check (they come from the API too): look again before removing.
            if (AdminManager.PlayerHasPermissions(new SteamID(current.SteamID), "@css/generic")) return;
            current.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
            LogAction($"Play access: removed {SanitizeName(current.PlayerName)} ({steamId}): {reason}.");
            Console.WriteLine($"[LEGACY-X Admin] Play access: removed {SanitizeName(current.PlayerName)} ({steamId}): {reason}.");
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }
}
