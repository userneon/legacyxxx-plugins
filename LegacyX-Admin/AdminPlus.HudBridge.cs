using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Linq;

namespace AdminPlus;

// The bridge to LegacyX-Hud's admin panel (!admin opens it when the Hud is loaded). The two plugins live in separate
// contexts, so they talk through server commands, like lx_hud_knife / lx_knife_choice:
//   lx_admin_do <actorSlot> <action> <targetSlot> [args…]     the panel's buttons; every action checks the actor's own rights
//   lx_admin_status <actorSlot> <targetSlot>                  answers with: lx_hud_admin status <actorSlot> <targetSlot> <mute 0|1> <gag 0|1>
// Only the server console can run them, and the actor is the staff member who clicked, so the permission checks, the
// announcements and the logs are the ones of the chat menu.
public partial class AdminPlus
{
    /// <summary>Commands the panel may run for an actor through RunServerCmd (which checks that actor's access to each).</summary>
    private static readonly string[] HudCommands = { "css_slay", "css_respawn", "css_team", "css_clean", "css_rr", "css_map", "css_unmute", "css_ungag", "css_unsilence", "css_unban" };

    private void RegisterHudBridge()
    {
        AddCommand("lx_admin_do", "LegacyX-Hud admin panel: lx_admin_do <actorSlot> <action> <targetSlot> [args]", HudDo);
        AddCommand("lx_admin_status", "LegacyX-Hud admin panel: lx_admin_status <actorSlot> <targetSlot>", HudStatus);
    }

    private static CCSPlayerController? PlayerAt(CommandInfo info, int index)
    {
        if (!int.TryParse(info.GetArg(index), out var slot)) return null;
        var player = Utilities.GetPlayerFromSlot(slot);
        return player is { IsValid: true } ? player : null;
    }

    private void HudDo(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null || info.ArgCount < 3) return;
        var actor = PlayerAt(info, 1);
        if (actor is null || actor.IsBot) return;
        var action = info.GetArg(2).ToLowerInvariant();

        if (action == "run")
        {
            // lx_admin_do <actor> run <command with its arguments>: only the commands above, never arbitrary text.
            var text = string.Join(' ', Enumerable.Range(3, Math.Max(0, info.ArgCount - 3)).Select(info.GetArg)).Trim();
            var token = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (!HudCommands.Contains(token) || text.Contains(';') || text.Contains('\n')) return;
            RunServerCmd(actor, text);
            return;
        }

        // Server-wide actions have no target.
        switch (action)
        {
            case "restartround":
                if (!HasEffectivePermission(actor, "@css/generic")) { actor.Print(Localizer["NoPermission"]); return; }
                Server.ExecuteCommand("mp_restartgame 1");
                PlayerExtensions.PrintToAll(Localizer["Round.Restarted", actor.PlayerName]);
                return;
            case "map":
                // lx_admin_do <actor> map <ignored> <map>
                var map = info.GetArg(4);
                if (!HasEffectivePermission(actor, "@css/generic")) { actor.Print(Localizer["NoPermission"]); return; }
                if (!PredefinedMaps.Contains(map)) return;
                if (Server.MapName == map) { actor.Print($"Already on {{white}}{map}{{grey}}."); return; }
                PlayerExtensions.PrintToAll(Localizer["Map.Changed", actor.PlayerName, map]);
                AddTimer(2.0f, () => Server.ExecuteCommand($"changelevel {map}"));
                return;
        }

        var target = PlayerAt(info, 3);
        if (target is null) { actor.Print("That player is not on the server any more."); return; }

        switch (action)
        {
            case "kick":
                if (!HasEffectivePermission(actor, "@css/generic")) { actor.Print(Localizer["NoPermission"]); return; }
                target.Disconnect(CounterStrikeSharp.API.ValveConstants.Protobuf.NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                PlayerExtensions.PrintToAll(Localizer["Player.Kick.Success", actor.PlayerName, SanitizeName(target.PlayerName), Localizer["Ban.NoReason"]]);
                LogAction($"{actor.PlayerName} kicked {SanitizeName(target.PlayerName)} from the admin panel.");
                break;

            case "ban" or "ipban":
                // lx_admin_do <actor> ban <target> <minutes> <reason…> (0 minutes = permanent)
                if (!int.TryParse(info.GetArg(4), out var minutes) || minutes < 0) return;
                var reason = string.Join(' ', Enumerable.Range(5, Math.Max(0, info.ArgCount - 5)).Select(info.GetArg)).Trim();
                if (reason.Length == 0) reason = Localizer["Ban.NoReason"];
                ApplyMenuBan(actor, new BanMenuTarget(target.SteamID, SanitizeName(target.PlayerName), string.IsNullOrWhiteSpace(target.IpAddress) ? "-" : target.IpAddress), minutes, action == "ipban", reason);
                break;

            case "mute" or "gag" or "silence":
                // lx_admin_do <actor> mute <target> <minutes> (0 = permanent)
                if (!HasEffectivePermission(actor, "@css/chat")) { actor.Print(Localizer["NoPermission"]); return; }
                if (!int.TryParse(info.GetArg(4), out var length) || length < 0) return;
                ApplyCommunicationPunishment(actor, target, action.ToUpperInvariant(), length);
                break;
        }
    }

    private void HudStatus(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null || info.ArgCount < 3) return;
        var actor = PlayerAt(info, 1);
        var target = PlayerAt(info, 2);
        if (actor is null || target is null) return;
        var muted = IsPlayerPunished(target.SteamID, "MUTE") || target.VoiceFlags == VoiceFlags.Muted;
        var gagged = IsPlayerPunished(target.SteamID, "GAG");
        Server.ExecuteCommand($"lx_hud_admin status {actor.Slot} {target.Slot} {(muted ? 1 : 0)} {(gagged ? 1 : 0)}");
    }
}
