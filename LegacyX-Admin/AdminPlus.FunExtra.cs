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
/// More fun commands (owner request 2026-11-06), all @css/slay, same targets as the other fun commands, and (like blind and freeze) one
/// command that switches the effect on and off:
///   css_invisible &lt;target&gt;            the player and the weapons they hold cannot be seen; run it again to show them
///   css_rainbow &lt;target&gt; [seconds]    the player's model cycles through the colours (default 30 s, up to 300); again to stop
///   css_spin &lt;target&gt; [seconds]       the player's view spins round (default 5 s, up to 60); again to stop
///   css_rocket &lt;target&gt;               the player is thrown high up and blows up a moment later
/// Every effect ends when the player spawns again.
/// </summary>
public partial class AdminPlus
{
    private readonly HashSet<ulong> _invisible = new();
    private readonly Dictionary<ulong, Timer> _rainbowTimers = new();
    private readonly Dictionary<ulong, Timer> _spinTimers = new();

    private void RegisterFunExtraCommands()
    {
        AddPermCmd("@css/slay", "css_invisible", "<#userid|name|all @ commands>", CmdInvisible);
        AddPermCmd("@css/slay", "css_rainbow", "<#userid|name|all @ commands> [seconds]", CmdRainbow);
        AddPermCmd("@css/slay", "css_spin", "<#userid|name|all @ commands> [seconds]", CmdSpin);
        AddPermCmd("@css/slay", "css_rocket", "<#userid|name|all @ commands>", CmdRocket);
        RegisterFunManyCommands();
    }

    /// <summary>The effects of this player end (they spawned again, or died).</summary>
    private void ClearFunExtra(ulong steamId)
    {
        _invisible.Remove(steamId);
        if (_rainbowTimers.Remove(steamId, out var rainbow)) { try { rainbow.Kill(); } catch { } }
        if (_spinTimers.Remove(steamId, out var spin)) { try { spin.Kill(); } catch { } }
        ClearFunMany(steamId);
        ClearFunWild(steamId);
    }

    private static bool Alive(CCSPlayerController p) => p.IsValid && p.PawnIsAlive && p.PlayerPawn?.Value is { IsValid: true };

    private bool ResolveFunTargets(CCSPlayerController? caller, CommandInfo info, string usage, string usageKey, out List<CCSPlayerController> players, out string admin, out string label)
    {
        players = new(); admin = ""; label = "";
        if (info.ArgCount < 2) { SendUsageMessage(caller, usageKey, usage); return false; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out players, out admin, out label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return false; }
        return true;
    }

    private static void SetVisible(CCSPlayerController target, bool visible)
    {
        var pawn = target.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid) return;
        var color = Color.FromArgb(visible ? 255 : 0, 255, 255, 255);
        pawn.Render = color;
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
        var weapons = pawn.WeaponServices?.MyWeapons;
        if (weapons == null) return;
        foreach (var handle in weapons)
        {
            var weapon = handle.Value;
            if (weapon == null || !weapon.IsValid) continue;
            weapon.Render = color;
            weapon.ShadowStrength = visible ? 1f : 0f;
            Utilities.SetStateChanged(weapon, "CBaseModelEntity", "m_clrRender");
        }
    }

    private void CmdInvisible(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_invisible <target>", "Fun.Invisible.Usage", out var players, out var admin, out var label)) return;
        var off = players.All(p => _invisible.Contains(p.SteamID));
        foreach (var t in players) { SetVisible(t, off); if (off) _invisible.Remove(t.SteamID); else _invisible.Add(t.SteamID); }
        Announce(off ? "css_invisible_off<player>" : "css_invisible<player>", off ? "css_invisible_off<multiple>" : "css_invisible<multiple>", admin, label, "");
        AdminPlus.LogAction($"{admin} {(off ? "showed" : "hid")} {label}");
    }

    private void CmdRainbow(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_rainbow <target> [seconds]", "Fun.Rainbow.Usage", out var players, out var admin, out var label)) return;
        var seconds = 30f;
        if (info.ArgCount >= 3 && float.TryParse(info.GetArg(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var asked)) seconds = Math.Clamp(asked, 1f, 300f);
        var off = players.All(p => _rainbowTimers.ContainsKey(p.SteamID));
        foreach (var t in players)
        {
            if (_rainbowTimers.Remove(t.SteamID, out var old)) { try { old.Kill(); } catch { } }
            if (off) { GlowPawn(t, Color.White); continue; }
            var started = Server.CurrentTime;
            var steam = t.SteamID;
            float hue = (steam % 360UL);
            _rainbowTimers[steam] = AddTimer(0.1f, () =>
            {
                if (!Alive(t) || Server.CurrentTime - started > seconds)
                {
                    if (_rainbowTimers.Remove(steam, out var done)) { try { done.Kill(); } catch { } }
                    if (t.IsValid && t.PlayerPawn?.Value is { IsValid: true }) GlowPawn(t, Color.White);
                    return;
                }
                hue = (hue + 12f) % 360f;
                GlowPawn(t, FromHue(hue));
            }, TimerFlags.REPEAT);
        }
        Announce(off ? "css_rainbow_off<player>" : "css_rainbow<player>", off ? "css_rainbow_off<multiple>" : "css_rainbow<multiple>", admin, label, off ? "" : seconds.ToString("0"));
        AdminPlus.LogAction($"{admin} {(off ? "stopped the rainbow on" : "made a rainbow of")} {label}");
    }

    /// <summary>A bright colour from a hue in degrees (full saturation and value).</summary>
    private static Color FromHue(float hue)
    {
        var h = hue / 60f;
        var x = (int)(255 * (1 - Math.Abs(h % 2 - 1)));
        return ((int)h % 6) switch
        {
            0 => Color.FromArgb(255, x, 0), 1 => Color.FromArgb(x, 255, 0), 2 => Color.FromArgb(0, 255, x),
            3 => Color.FromArgb(0, x, 255), 4 => Color.FromArgb(x, 0, 255), _ => Color.FromArgb(255, 0, x),
        };
    }

    private void CmdSpin(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_spin <target> [seconds]", "Fun.Spin.Usage", out var players, out var admin, out var label)) return;
        var seconds = 5f;
        if (info.ArgCount >= 3 && float.TryParse(info.GetArg(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var asked)) seconds = Math.Clamp(asked, 1f, 60f);
        var off = players.All(p => _spinTimers.ContainsKey(p.SteamID));
        foreach (var t in players)
        {
            if (_spinTimers.Remove(t.SteamID, out var old)) { try { old.Kill(); } catch { } }
            if (off) continue;
            var started = Server.CurrentTime;
            var steam = t.SteamID;
            _spinTimers[steam] = AddTimer(0.02f, () =>
            {
                var pawn = t.IsValid ? t.PlayerPawn?.Value : null;
                if (!Alive(t) || pawn == null || Server.CurrentTime - started > seconds)
                {
                    if (_spinTimers.Remove(steam, out var done)) { try { done.Kill(); } catch { } }
                    return;
                }
                var angles = pawn.EyeAngles;
                pawn.Teleport(null, new QAngle(angles.X, (angles.Y + 9f + 360f) % 360f, 0), null);
            }, TimerFlags.REPEAT);
        }
        Announce(off ? "css_spin_off<player>" : "css_spin<player>", off ? "css_spin_off<multiple>" : "css_spin<multiple>", admin, label, off ? "" : seconds.ToString("0"));
        AdminPlus.LogAction($"{admin} {(off ? "stopped the spin of" : "spun")} {label}");
    }

    private void CmdRocket(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_rocket <target>", "Fun.Rocket.Usage", out var players, out var admin, out var label)) return;
        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid) continue;
            var velocity = pawn.AbsVelocity;
            pawn.Teleport(null, null, new Vector(velocity.X * 0.2f, velocity.Y * 0.2f, LaunchMax * 1.6f));
            AddTimer(1.6f, () => { if (Alive(t)) t.PlayerPawn!.Value!.CommitSuicide(true, true); });
        }
        Announce("css_rocket<player>", "css_rocket<multiple>", admin, label, "");
        AdminPlus.LogAction($"{admin} sent {label} up in a rocket");
    }
}
