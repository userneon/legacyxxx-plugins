using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminPlus;

/// <summary>
/// The ban screen (owner request 2026-11-06, "when a ban is given a full-screen red HUD shows"): a banned player who is on a team sees the whole screen red with
/// the length and the reason (LegacyX-Hud `lx_troll_hud <slot> banned`, layout legacyx_troll.xml in the Workshop addon) for a few seconds, and is disconnected after it.
/// The ban itself is stored first, so reconnecting is refused whatever happens here. A player without a team, or without the HUD, is removed at once.
/// </summary>
public partial class AdminPlus
{
    private const float BanScreenSeconds = 4f;
    private readonly HashSet<ulong> banScreenPending = new();

    private static string BanScreenText(string durationText, string reason)
    {
        var allowed = " .,!?'+-:()";
        string Clean(string text) => new string((text ?? "").Where(ch => char.IsLetterOrDigit(ch) || allowed.Contains(ch)).ToArray()).Trim();
        var line = $"{Clean(durationText)}. {Clean(reason)}".Trim(' ', '.');
        return line.Length > 70 ? line[..70] : line;
    }

    /// <summary>Shows the ban screen to <paramref name="target"/>, then disconnects them with the Steam-banned reason.</summary>
    private void DisconnectBanned(CCSPlayerController target, string durationText, string reason)
    {
        if (target == null || !target.IsValid) return;
        var steamId = target.SteamID;
        var slot = target.Slot;
        if (target.IsBot || target.TeamNum is not (2 or 3))
        {
            target.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
            return;
        }
        if (!banScreenPending.Add(steamId)) return;
        FreezeForBan(target);
        try { Server.ExecuteCommand($"lx_troll_hud {slot} banned {BanScreenSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} \"{BanScreenText(durationText, reason)}\""); }
        catch (Exception exception) { LogError($"ban screen: {exception.Message}"); }
        AddTimer(BanScreenSeconds, () =>
        {
            banScreenPending.Remove(steamId);
            if (Utilities.GetPlayerFromSlot(slot) is { IsValid: true } again && again.SteamID == steamId)
                again.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    /// <summary>
    /// Holds a banned player where they stand until they are disconnected (owner request 2026-11-06: "freeze, the HUD, then the ban"): no movement, no weapons, the
    /// view is theirs. The same hold as css_freeze; it ends with the disconnect (the timer is dropped when the player leaves).
    /// </summary>
    private void FreezeForBan(CCSPlayerController target)
    {
        try
        {
            var pawn = target.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid || !target.PawnIsAlive) return;
            var anchor = pawn.AbsOrigin == null ? null : new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z);
            try { pawn.MoveType = MoveType_t.MOVETYPE_NONE; } catch { }
            SetActualMoveType(pawn, (int)MoveType_t.MOVETYPE_NONE);
            pawn.VelocityModifier = 0f; Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
            try { target.RemoveWeapons(); } catch { }
            StopTimer(target, FunTimer.FreezeLoop);
            RememberTimer(target, FunTimer.FreezeLoop, AddTimer(0.05f, () =>
            {
                if (!target.IsValid) { StopTimer(target, FunTimer.FreezeLoop); return; }
                var p = target.PlayerPawn?.Value; if (p == null || !p.IsValid) return;
                try { p.MoveType = MoveType_t.MOVETYPE_NONE; } catch { }
                SetActualMoveType(p, (int)MoveType_t.MOVETYPE_NONE);
                p.VelocityModifier = 0f;
                Utilities.SetStateChanged(p, "CCSPlayerPawn", "m_flVelocityModifier");
                Utilities.SetStateChanged(p, "CBaseEntity", "m_MoveType");
                if (anchor != null) p.Teleport(anchor, null, new Vector(0, 0, 0));
            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE));
        }
        catch (Exception exception) { LogError($"ban freeze: {exception.Message}"); }
    }
}

