using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace AdminPlus;

/// <summary>
/// Seven more fun commands (owner request 2026-11-06), all @css/slay with the same targets as the other fun commands. Those that
/// switch something on (burn, bounce, lowgrav) switch it off when run again; every effect ends when the player spawns again.
///   css_burn &lt;target&gt; [seconds]       the player takes fire damage (4 hp every half second) and glows orange (default 5 s, up to 30)
///   css_bounce &lt;target&gt; [seconds]     the player springs up again every time they touch the ground (default 20 s, up to 120)
///   css_lowgrav &lt;target&gt; [scale]      only that player falls slowly (scale 0.1 to 5, default 0.25; 1 puts it back)
///   css_fling &lt;target&gt;                throws the player the way the admin is looking
///   css_blink &lt;target&gt; [distance]     moves the player forward (100 to 1000 units, default 350) the way they are looking
///   css_tprandom &lt;target&gt;             moves the player to a random living player
///   css_drop &lt;target&gt;                 makes the player drop the weapon in their hands
/// </summary>
public partial class AdminPlus
{
    private readonly Dictionary<ulong, Timer> _burnTimers = new();
    private readonly Dictionary<ulong, Timer> _bounceTimers = new();
    private readonly HashSet<ulong> _lowGrav = new();
    private static readonly Random FunRandom = new();

    private void RegisterFunManyCommands()
    {
        AddPermCmd("@css/slay", "css_burn", "<#userid|name|all @ commands> [seconds]", CmdBurn);
        AddPermCmd("@css/slay", "css_bounce", "<#userid|name|all @ commands> [seconds]", CmdBounce);
        AddPermCmd("@css/slay", "css_lowgrav", "<#userid|name|all @ commands> [scale]", CmdLowGrav);
        AddPermCmd("@css/slay", "css_fling", "<#userid|name|all @ commands>", CmdFling);
        AddPermCmd("@css/slay", "css_blink", "<#userid|name|all @ commands> [distance]", CmdBlink);
        AddPermCmd("@css/slay", "css_tprandom", "<#userid|name|all @ commands>", CmdTpRandom);
        AddPermCmd("@css/slay", "css_drop", "<#userid|name|all @ commands>", CmdDrop);
    }

    private void ClearFunMany(ulong steamId)
    {
        if (_burnTimers.Remove(steamId, out var burn)) { try { burn.Kill(); } catch { } }
        if (_bounceTimers.Remove(steamId, out var bounce)) { try { bounce.Kill(); } catch { } }
        _lowGrav.Remove(steamId);
    }

    private static float? SecondsArg(CommandInfo info, int index, float fallback, float min, float max)
    {
        if (info.ArgCount <= index) return fallback;
        return float.TryParse(info.GetArg(index), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, min, max) : null;
    }

    private void CmdBurn(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_burn <target> [seconds]", "Fun.Burn.Usage", out var players, out var admin, out var label)) return;
        var seconds = SecondsArg(info, 2, 5f, 1f, 30f) ?? 5f;
        var off = players.All(p => _burnTimers.ContainsKey(p.SteamID));
        foreach (var t in players)
        {
            if (_burnTimers.Remove(t.SteamID, out var old)) { try { old.Kill(); } catch { } }
            if (off) { GlowPawn(t, Color.White); continue; }
            var started = Server.CurrentTime;
            var steam = t.SteamID;
            GlowPawn(t, Color.OrangeRed);
            _burnTimers[steam] = AddTimer(0.5f, () =>
            {
                if (!Alive(t) || Server.CurrentTime - started > seconds)
                {
                    if (_burnTimers.Remove(steam, out var done)) { try { done.Kill(); } catch { } }
                    if (t.IsValid && t.PlayerPawn?.Value is { IsValid: true }) GlowPawn(t, Color.White);
                    return;
                }
                var pawn = t.PlayerPawn!.Value!;
                if (pawn.Health <= 4) { pawn.CommitSuicide(false, true); return; }
                SetHealth(t, pawn.Health - 4);
            }, TimerFlags.REPEAT);
        }
        Announce(off ? "css_burn_off<player>" : "css_burn<player>", off ? "css_burn_off<multiple>" : "css_burn<multiple>", admin, label, off ? "" : seconds.ToString("0"));
        AdminPlus.LogAction($"{admin} {(off ? "put out" : "set on fire")} {label}");
    }

    private void CmdBounce(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_bounce <target> [seconds]", "Fun.Bounce.Usage", out var players, out var admin, out var label)) return;
        var seconds = SecondsArg(info, 2, 20f, 1f, 120f) ?? 20f;
        var off = players.All(p => _bounceTimers.ContainsKey(p.SteamID));
        foreach (var t in players)
        {
            if (_bounceTimers.Remove(t.SteamID, out var old)) { try { old.Kill(); } catch { } }
            if (off) continue;
            var started = Server.CurrentTime;
            var steam = t.SteamID;
            _bounceTimers[steam] = AddTimer(0.1f, () =>
            {
                var pawn = t.IsValid ? t.PlayerPawn?.Value : null;
                if (!Alive(t) || pawn == null || Server.CurrentTime - started > seconds)
                {
                    if (_bounceTimers.Remove(steam, out var done)) { try { done.Kill(); } catch { } }
                    return;
                }
                if ((pawn.Flags & 1) == 0) return;   // only when they stand on the ground
                var velocity = pawn.AbsVelocity;
                pawn.Teleport(null, null, new Vector(velocity.X, velocity.Y, 520f));
            }, TimerFlags.REPEAT);
        }
        Announce(off ? "css_bounce_off<player>" : "css_bounce<player>", off ? "css_bounce_off<multiple>" : "css_bounce<multiple>", admin, label, off ? "" : seconds.ToString("0"));
        AdminPlus.LogAction($"{admin} {(off ? "stopped the bouncing of" : "made bounce")} {label}");
    }

    private void CmdLowGrav(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_lowgrav <target> [scale]", "Fun.LowGrav.Usage", out var players, out var admin, out var label)) return;
        var scale = SecondsArg(info, 2, 0.25f, 0.1f, 5f) ?? 0.25f;
        var off = info.ArgCount < 3 && players.All(p => _lowGrav.Contains(p.SteamID)) || Math.Abs(scale - 1f) < 0.001f;
        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid) continue;
            pawn.GravityScale = off ? 1f : scale;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_flGravityScale");
            if (off) _lowGrav.Remove(t.SteamID); else _lowGrav.Add(t.SteamID);
        }
        Announce(off ? "css_lowgrav_off<player>" : "css_lowgrav<player>", off ? "css_lowgrav_off<multiple>" : "css_lowgrav<multiple>", admin, label, off ? "" : scale.ToString("0.##"));
        AdminPlus.LogAction(off ? $"{admin} restored the gravity of {label}" : $"{admin} set the gravity of {label} to {scale:0.##}");
    }

    private void CmdFling(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_fling <target>", "Fun.Fling.Usage", out var players, out var admin, out var label)) return;
        // The way the admin looks; from the console, a random way.
        var angles = caller != null && caller.IsValid && caller.PlayerPawn?.Value is { IsValid: true } own ? own.EyeAngles : new QAngle(-20f, (float)FunRandom.NextDouble() * 360f, 0);
        var pitch = angles.X * MathF.PI / 180f; var yaw = angles.Y * MathF.PI / 180f;
        var force = new Vector(MathF.Cos(pitch) * MathF.Cos(yaw) * 1400f, MathF.Cos(pitch) * MathF.Sin(yaw) * 1400f, -MathF.Sin(pitch) * 1400f + 300f);
        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid) continue;
            pawn.Teleport(null, null, force);
        }
        Announce("css_fling<player>", "css_fling<multiple>", admin, label, "");
        AdminPlus.LogAction($"{admin} flung {label}");
    }

    private void CmdBlink(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_blink <target> [distance]", "Fun.Blink.Usage", out var players, out var admin, out var label)) return;
        var distance = SecondsArg(info, 2, 350f, 100f, 1000f) ?? 350f;
        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) continue;
            var yaw = pawn.EyeAngles.Y * MathF.PI / 180f;
            var origin = pawn.AbsOrigin;
            pawn.Teleport(new Vector(origin.X + MathF.Cos(yaw) * distance, origin.Y + MathF.Sin(yaw) * distance, origin.Z + 12f), null, new Vector(0, 0, 0));
        }
        Announce("css_blink<player>", "css_blink<multiple>", admin, label, "");
        AdminPlus.LogAction($"{admin} blinked {label} {distance:0} units");
    }

    private void CmdTpRandom(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_tprandom <target>", "Fun.TpRandom.Usage", out var players, out var admin, out var label)) return;
        foreach (var t in players)
        {
            var others = Utilities.GetPlayers().Where(p => p.IsValid && p.Slot != t.Slot && Alive(p) && p.PlayerPawn!.Value!.AbsOrigin != null).ToList();
            var pawn = t.PlayerPawn?.Value;
            if (others.Count == 0 || pawn == null || !pawn.IsValid) continue;
            var spot = others[FunRandom.Next(others.Count)].PlayerPawn!.Value!.AbsOrigin!;
            pawn.Teleport(new Vector(spot.X, spot.Y, spot.Z + 12f), null, new Vector(0, 0, 0));
        }
        Announce("css_tprandom<player>", "css_tprandom<multiple>", admin, label, "");
        AdminPlus.LogAction($"{admin} sent {label} to a random player");
    }

    private void CmdDrop(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_drop <target>", "Fun.Drop.Usage", out var players, out var admin, out var label)) return;
        foreach (var t in players) { try { t.DropActiveWeapon(); } catch { } }
        Announce("css_drop<player>", "css_drop<multiple>", admin, label, "");
        AdminPlus.LogAction($"{admin} made {label} drop their weapon");
    }
}
