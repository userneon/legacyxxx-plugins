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
/// A big batch of fun commands (owner request 2026-11-06, "as many as possible, and some that use the Workshop addon"), all @css/slay with the same
/// targets as the other fun commands. Those that switch something on switch it off when run again; every effect ends when the player spawns again.
/// UNTESTED in game: they are written from what CounterStrikeSharp offers, so try each on a test server first.
///   css_jetpack &lt;t&gt; [s]        hold jump to fly                css_doublejump &lt;t&gt; [s]   a second jump in the air
///   css_sky &lt;t&gt; [height]       up into the sky, then falls     css_pull &lt;t&gt;             pulled to the admin
///   css_shockwave &lt;t&gt; [radius] everyone near them is thrown    css_explode &lt;t&gt; [radius] they die, the ones near are thrown and hurt
///   css_vampire &lt;t&gt; [s]        +50 hp on every kill            css_infammo &lt;t&gt; [s]      the magazine never runs out
///   css_gift &lt;t&gt;               a random gun or grenade         css_fov &lt;t&gt; [60-150]     wide or narrow view (90 puts it back)
///   css_ghost &lt;t&gt;              see-through                     css_chicken &lt;t&gt;          the player looks like a chicken
///   css_pop &lt;t&gt;                a burst of the addon's effect   css_aura &lt;t&gt; [shape] [s]  a trail of the addon's effects behind the player
///   css_roulette                a random living player gets a random effect
///   css_chaos [s]               roulette every few seconds, again to stop
/// </summary>
public partial class AdminPlus
{
    private readonly Dictionary<ulong, Timer> _jetpackTimers = new();
    private readonly Dictionary<ulong, Timer> _doubleJumpTimers = new();
    private readonly Dictionary<ulong, Timer> _auraTimers = new();
    private readonly HashSet<ulong> _vampire = new();
    private readonly HashSet<ulong> _infAmmo = new();
    private readonly HashSet<ulong> _ghost = new();
    private Timer? _chaosTimer;

    private const string FxBurst = "particles/legacyx/bounce_burst.vpcf";
    private static readonly Dictionary<string, string> FxTrails = new()
    {
        ["pulse"] = "particles/legacyx/trail_pulse.vpcf",
        ["ribbon"] = "particles/legacyx/trail_ribbon.vpcf",
        ["lightning"] = "particles/legacyx/trail_lightning.vpcf",
        ["stardust"] = "particles/legacyx/trail_stardust.vpcf",
    };
    private const string ChickenModel = "models/chicken/chicken.vmdl";
    private static readonly string[] GiftWeapons =
    {
        "weapon_ak47", "weapon_m4a1", "weapon_m4a1_silencer", "weapon_awp", "weapon_deagle", "weapon_mp9", "weapon_p90", "weapon_negev", "weapon_nova",
        "weapon_xm1014", "weapon_aug", "weapon_famas", "weapon_galilar", "weapon_ssg08", "weapon_scar20", "weapon_mac10", "weapon_ump45", "weapon_bizon",
        "weapon_mag7", "weapon_p250", "weapon_tec9", "weapon_fiveseven", "weapon_cz75a", "weapon_elite", "weapon_taser", "weapon_hegrenade",
        "weapon_flashbang", "weapon_smokegrenade", "weapon_molotov", "weapon_decoy",
    };
    private static readonly string[] RouletteEffects = { "burn", "bounce", "spin", "fling", "blink", "rocket", "drug", "shake", "sky", "pop", "chicken", "ghost", "jetpack", "doublejump" };

    private void RegisterFunWildCommands()
    {
        AddPermCmd("@css/slay", "css_jetpack", "<#userid|name|all @ commands> [seconds]", CmdJetpack);
        AddPermCmd("@css/slay", "css_doublejump", "<#userid|name|all @ commands> [seconds]", CmdDoubleJump);
        AddPermCmd("@css/slay", "css_sky", "<#userid|name|all @ commands> [height]", CmdSky);
        AddPermCmd("@css/slay", "css_pull", "<#userid|name|all @ commands>", CmdPull);
        AddPermCmd("@css/slay", "css_shockwave", "<#userid|name|all @ commands> [radius]", CmdShockwave);
        AddPermCmd("@css/slay", "css_explode", "<#userid|name|all @ commands> [radius]", CmdExplode);
        AddPermCmd("@css/slay", "css_vampire", "<#userid|name|all @ commands> [seconds]", CmdVampire);
        AddPermCmd("@css/slay", "css_infammo", "<#userid|name|all @ commands> [seconds]", CmdInfAmmo);
        AddPermCmd("@css/slay", "css_gift", "<#userid|name|all @ commands>", CmdGift);
        AddPermCmd("@css/slay", "css_fov", "<#userid|name|all @ commands> [60-150]", CmdFov);
        AddPermCmd("@css/slay", "css_ghost", "<#userid|name|all @ commands>", CmdGhost);
        AddPermCmd("@css/slay", "css_chicken", "<#userid|name|all @ commands>", CmdChicken);
        AddPermCmd("@css/slay", "css_pop", "<#userid|name|all @ commands>", CmdPop);
        AddPermCmd("@css/slay", "css_aura", "<#userid|name|all @ commands> [pulse|ribbon|lightning|stardust] [seconds]", CmdAura);
        AddPermCmd("@css/slay", "css_roulette", "", CmdRoulette);
        AddPermCmd("@css/slay", "css_chaos", "[seconds]", CmdChaos);

        // The Workshop addon's effects and the chicken must be precached when the map loads, or spawning them does nothing.
        RegisterListener<Listeners.OnServerPrecacheResources>(manifest =>
        {
            manifest.AddResource(FxBurst);
            foreach (var effect in FxTrails.Values) manifest.AddResource(effect);
            manifest.AddResource(ChickenModel);
        });
        RegisterEventHandler<EventPlayerDeath>((@event, _) =>
        {
            var killer = @event.Attacker;
            if (killer is { IsValid: true } && _vampire.Contains(killer.SteamID) && @event.Userid?.SteamID != killer.SteamID && Alive(killer))
                SetHealth(killer, Math.Min(100, killer.PlayerPawn!.Value!.Health + 50));
            return HookResult.Continue;
        });
        RegisterEventHandler<EventWeaponFire>((@event, _) =>
        {
            if (@event.Userid is not { IsValid: true } shooter || !_infAmmo.Contains(shooter.SteamID)) return HookResult.Continue;
            try
            {
                var weapon = shooter.PlayerPawn?.Value?.WeaponServices?.ActiveWeapon.Value;
                if (weapon is not { IsValid: true }) return HookResult.Continue;
                var gun = weapon.As<CCSWeaponBase>();
                var max = gun.VData?.MaxClip1 ?? 0;
                if (max <= 0) return HookResult.Continue;
                gun.Clip1 = max;
                Utilities.SetStateChanged(gun, "CBasePlayerWeapon", "m_iClip1");
            }
            catch { }
            return HookResult.Continue;
        });
    }

    private void ClearFunWild(ulong steamId)
    {
        foreach (var map in new[] { _jetpackTimers, _doubleJumpTimers, _auraTimers })
            if (map.Remove(steamId, out var timer)) { try { timer.Kill(); } catch { } }
        _vampire.Remove(steamId);
        _infAmmo.Remove(steamId);
        _ghost.Remove(steamId);
    }

    private static Vector Velocity(CCSPlayerController p) => p.PlayerPawn?.Value?.AbsVelocity is { } v ? new Vector(v.X, v.Y, v.Z) : new Vector(0, 0, 0);
    private static bool OnGround(CCSPlayerController p) => p.PlayerPawn?.Value is { } pawn && (pawn.Flags & 1) != 0;
    private static bool HoldsJump(CCSPlayerController p) => (p.Buttons & PlayerButtons.Jump) != 0;
    private static Color RainbowAt(double phase)
    {
        var h = (phase % 1.0) * 6.0; var f = h - Math.Floor(h); var q = (int)(255 * (1 - f)); var t = (int)(255 * f);
        return ((int)h) switch { 0 => Color.FromArgb(255, t, 0), 1 => Color.FromArgb(q, 255, 0), 2 => Color.FromArgb(0, 255, t), 3 => Color.FromArgb(0, q, 255), 4 => Color.FromArgb(t, 0, 255), _ => Color.FromArgb(255, 0, q) };
    }

    /// <summary>One of the Workshop addon's effects at `at`, tinted through control point 16 and removed after `seconds` (nothing happens when it cannot be spawned).</summary>
    private CParticleSystem? SpawnFx(string effectName, Vector at, Color color, float seconds)
    {
        try
        {
            var particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
            if (particle == null || !particle.IsValid) return null;
            particle.EffectName = effectName;
            particle.StartActive = true;
            particle.DataCP = 16;
            particle.DataCPValue.X = color.R; particle.DataCPValue.Y = color.G; particle.DataCPValue.Z = color.B;
            particle.Teleport(at, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            particle.DispatchSpawn();
            AddTimer(seconds, () => { if (particle.IsValid) particle.Remove(); });
            return particle;
        }
        catch (Exception exception) { LogError($"effect {effectName}: {exception.Message}"); return null; }
    }

    private static void ThrowFrom(CCSPlayerController player, Vector from, float strength, float lift)
    {
        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.AbsOrigin is not { } at) return;
        var dx = at.X - from.X; var dy = at.Y - from.Y;
        var length = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        pawn.Teleport(null, null, new Vector(dx / length * strength, dy / length * strength, lift));
    }

    // ---- movement -----------------------------------------------------------------------------------

    private void CmdJetpack(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_jetpack <target> [seconds]", "Fun.Jetpack.Usage", out var players, out var admin, out var label)) return;
        var seconds = SecondsArg(info, 2, 30f, 2f, 180f) ?? 30f;
        var off = players.All(p => _jetpackTimers.ContainsKey(p.SteamID));
        foreach (var t in players)
        {
            if (_jetpackTimers.Remove(t.SteamID, out var old)) { try { old.Kill(); } catch { } }
            if (off) continue;
            var started = Server.CurrentTime; var steam = t.SteamID;
            _jetpackTimers[steam] = AddTimer(0.03f, () =>
            {
                if (!Alive(t) || Server.CurrentTime - started > seconds)
                {
                    if (_jetpackTimers.Remove(steam, out var done)) { try { done.Kill(); } catch { } }
                    return;
                }
                if (!HoldsJump(t) || OnGround(t)) return;
                var v = Velocity(t);
                t.PlayerPawn!.Value!.Teleport(null, null, new Vector(v.X, v.Y, MathF.Min(v.Z + 70f, 380f)));
            }, TimerFlags.REPEAT);
        }
        Announce(off ? "css_jetpack_off<player>" : "css_jetpack<player>", off ? "css_jetpack_off<multiple>" : "css_jetpack<multiple>", admin, label, off ? "" : seconds.ToString("0"));
        AdminPlus.LogAction($"{admin} {(off ? "took the jetpack off" : "gave a jetpack to")} {label}");
    }

    private void CmdDoubleJump(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_doublejump <target> [seconds]", "Fun.DoubleJump.Usage", out var players, out var admin, out var label)) return;
        var seconds = SecondsArg(info, 2, 60f, 2f, 300f) ?? 60f;
        var off = players.All(p => _doubleJumpTimers.ContainsKey(p.SteamID));
        foreach (var t in players)
        {
            if (_doubleJumpTimers.Remove(t.SteamID, out var old)) { try { old.Kill(); } catch { } }
            if (off) continue;
            var started = Server.CurrentTime; var steam = t.SteamID;
            var used = false; var wasHolding = false;
            _doubleJumpTimers[steam] = AddTimer(0.02f, () =>
            {
                if (!Alive(t) || Server.CurrentTime - started > seconds)
                {
                    if (_doubleJumpTimers.Remove(steam, out var done)) { try { done.Kill(); } catch { } }
                    return;
                }
                var holding = HoldsJump(t);
                if (OnGround(t)) used = false;
                else if (holding && !wasHolding && !used)
                {
                    used = true;
                    var v = Velocity(t);
                    t.PlayerPawn!.Value!.Teleport(null, null, new Vector(v.X, v.Y, 330f));
                }
                wasHolding = holding;
            }, TimerFlags.REPEAT);
        }
        Announce(off ? "css_doublejump_off<player>" : "css_doublejump<player>", off ? "css_doublejump_off<multiple>" : "css_doublejump<multiple>", admin, label, off ? "" : seconds.ToString("0"));
        AdminPlus.LogAction($"{admin} {(off ? "took the double jump from" : "gave a double jump to")} {label}");
    }

    private void CmdSky(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_sky <target> [height]", "Fun.Sky.Usage", out var players, out var admin, out var label)) return;
        var height = SecondsArg(info, 2, 1500f, 300f, 4000f) ?? 1500f;
        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin is not { } o) continue;
            pawn.Teleport(new Vector(o.X, o.Y, o.Z + height), null, new Vector(0, 0, 0));
        }
        Announce("css_sky<player>", "css_sky<multiple>", admin, label, height.ToString("0"));
        AdminPlus.LogAction($"{admin} sent {label} into the sky");
    }

    private void CmdPull(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_pull <target>", "Fun.Pull.Usage", out var players, out var admin, out var label)) return;
        if (caller == null || !Alive(caller) || caller.PlayerPawn!.Value!.AbsOrigin is not { } to) { LogError("css_pull needs a living admin to pull towards"); return; }
        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin is not { } at || t.Slot == caller.Slot) continue;
            var dx = to.X - at.X; var dy = to.Y - at.Y;
            var length = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
            pawn.Teleport(null, null, new Vector(dx / length * 1300f, dy / length * 1300f, 260f));
        }
        Announce("css_pull<player>", "css_pull<multiple>", admin, label, "");
        AdminPlus.LogAction($"{admin} pulled {label} towards themselves");
    }

    private void CmdShockwave(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_shockwave <target> [radius]", "Fun.Shockwave.Usage", out var players, out var admin, out var label)) return;
        var radius = SecondsArg(info, 2, 400f, 100f, 1500f) ?? 400f;
        foreach (var t in players)
        {
            if (t.PlayerPawn?.Value?.AbsOrigin is not { } centre) continue;
            SpawnFx(FxBurst, new Vector(centre.X, centre.Y, centre.Z + 10f), Color.White, 2f);
            foreach (var other in Utilities.GetPlayers().Where(p => p.IsValid && p.Slot != t.Slot && Alive(p)))
            {
                if (other.PlayerPawn!.Value!.AbsOrigin is not { } at) continue;
                var dx = at.X - centre.X; var dy = at.Y - centre.Y; var dz = at.Z - centre.Z;
                if (dx * dx + dy * dy + dz * dz > radius * radius) continue;
                ThrowFrom(other, centre, 900f, 420f);
            }
        }
        Announce("css_shockwave<player>", "css_shockwave<multiple>", admin, label, radius.ToString("0"));
        AdminPlus.LogAction($"{admin} sent a shockwave from {label}");
    }

    private void CmdExplode(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_explode <target> [radius]", "Fun.Explode.Usage", out var players, out var admin, out var label)) return;
        var radius = SecondsArg(info, 2, 300f, 100f, 1000f) ?? 300f;
        foreach (var t in players.ToList())
        {
            if (!Alive(t) || t.PlayerPawn!.Value!.AbsOrigin is not { } centre) continue;
            var at = new Vector(centre.X, centre.Y, centre.Z);
            SpawnFx(FxBurst, new Vector(at.X, at.Y, at.Z + 20f), Color.OrangeRed, 2f);
            foreach (var other in Utilities.GetPlayers().Where(p => p.IsValid && p.Slot != t.Slot && Alive(p)))
            {
                if (other.PlayerPawn!.Value!.AbsOrigin is not { } o) continue;
                var dx = o.X - at.X; var dy = o.Y - at.Y; var dz = o.Z - at.Z;
                if (dx * dx + dy * dy + dz * dz > radius * radius) continue;
                var pawn = other.PlayerPawn!.Value!;
                if (pawn.Health <= 40) pawn.CommitSuicide(false, true); else SetHealth(other, pawn.Health - 40);
                ThrowFrom(other, at, 800f, 400f);
            }
            t.PlayerPawn!.Value!.CommitSuicide(true, true);
        }
        Announce("css_explode<player>", "css_explode<multiple>", admin, label, radius.ToString("0"));
        AdminPlus.LogAction($"{admin} blew up {label}");
    }

    // ---- combat -------------------------------------------------------------------------------------

    private void CmdVampire(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_vampire <target> [seconds]", "Fun.Vampire.Usage", out var players, out var admin, out var label)) return;
        var seconds = SecondsArg(info, 2, 60f, 5f, 600f) ?? 60f;
        var off = players.All(p => _vampire.Contains(p.SteamID));
        foreach (var t in players)
        {
            var steam = t.SteamID;
            if (off) { _vampire.Remove(steam); continue; }
            _vampire.Add(steam);
            AddTimer(seconds, () => _vampire.Remove(steam));
        }
        Announce(off ? "css_vampire_off<player>" : "css_vampire<player>", off ? "css_vampire_off<multiple>" : "css_vampire<multiple>", admin, label, off ? "" : seconds.ToString("0"));
        AdminPlus.LogAction($"{admin} {(off ? "took the vampire from" : "made a vampire of")} {label}");
    }

    private void CmdInfAmmo(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_infammo <target> [seconds]", "Fun.InfAmmo.Usage", out var players, out var admin, out var label)) return;
        var seconds = SecondsArg(info, 2, 60f, 5f, 600f) ?? 60f;
        var off = players.All(p => _infAmmo.Contains(p.SteamID));
        foreach (var t in players)
        {
            var steam = t.SteamID;
            if (off) { _infAmmo.Remove(steam); continue; }
            _infAmmo.Add(steam);
            AddTimer(seconds, () => _infAmmo.Remove(steam));
        }
        Announce(off ? "css_infammo_off<player>" : "css_infammo<player>", off ? "css_infammo_off<multiple>" : "css_infammo<multiple>", admin, label, off ? "" : seconds.ToString("0"));
        AdminPlus.LogAction($"{admin} {(off ? "took the endless magazine from" : "gave an endless magazine to")} {label}");
    }

    private void CmdGift(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_gift <target>", "Fun.Gift.Usage", out var players, out var admin, out var label)) return;
        foreach (var t in players)
        {
            if (!Alive(t)) continue;
            try { t.GiveNamedItem(GiftWeapons[FunRandom.Next(GiftWeapons.Length)]); } catch { }
        }
        Announce("css_gift<player>", "css_gift<multiple>", admin, label, "");
        AdminPlus.LogAction($"{admin} gave a random weapon to {label}");
    }

    // ---- looks --------------------------------------------------------------------------------------

    private void CmdFov(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_fov <target> [60-150]", "Fun.Fov.Usage", out var players, out var admin, out var label)) return;
        var fov = (uint)Math.Round(SecondsArg(info, 2, 130f, 60f, 150f) ?? 130f);
        foreach (var t in players)
        {
            t.DesiredFOV = fov;
            Utilities.SetStateChanged(t, "CBasePlayerController", "m_iDesiredFOV");
        }
        Announce("css_fov<player>", "css_fov<multiple>", admin, label, fov.ToString());
        AdminPlus.LogAction($"{admin} set the view of {label} to {fov}");
    }

    private static void SetAlpha(CCSPlayerController target, int alpha)
    {
        var pawn = target.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid) return;
        var color = Color.FromArgb(alpha, 255, 255, 255);
        pawn.Render = color;
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
    }

    private void CmdGhost(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_ghost <target>", "Fun.Ghost.Usage", out var players, out var admin, out var label)) return;
        var off = players.All(p => _ghost.Contains(p.SteamID));
        foreach (var t in players)
        {
            if (off) { _ghost.Remove(t.SteamID); SetAlpha(t, 255); }
            else { _ghost.Add(t.SteamID); SetAlpha(t, 70); }
        }
        Announce(off ? "css_ghost_off<player>" : "css_ghost<player>", off ? "css_ghost_off<multiple>" : "css_ghost<multiple>", admin, label, "");
        AdminPlus.LogAction($"{admin} {(off ? "made solid" : "made a ghost of")} {label}");
    }

    private void CmdChicken(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_chicken <target>", "Fun.Chicken.Usage", out var players, out var admin, out var label)) return;
        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid) continue;
            try { pawn.SetModel(ChickenModel); } catch (Exception exception) { LogError($"chicken: {exception.Message}"); }
        }
        Announce("css_chicken<player>", "css_chicken<multiple>", admin, label, "");
        AdminPlus.LogAction($"{admin} turned {label} into a chicken (until they spawn again)");
    }

    // ---- the Workshop addon's effects ---------------------------------------------------------------

    private void CmdPop(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_pop <target>", "Fun.Pop.Usage", out var players, out var admin, out var label)) return;
        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin is not { } o) continue;
            SpawnFx(FxBurst, new Vector(o.X, o.Y, o.Z + 10f), RainbowAt(FunRandom.NextDouble()), 2f);
            pawn.Teleport(null, null, new Vector(0, 0, 480f));
        }
        Announce("css_pop<player>", "css_pop<multiple>", admin, label, "");
        AdminPlus.LogAction($"{admin} popped {label}");
    }

    private void CmdAura(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ResolveFunTargets(caller, info, "Usage: css_aura <target> [pulse|ribbon|lightning|stardust] [seconds]", "Fun.Aura.Usage", out var players, out var admin, out var label)) return;
        var shape = info.ArgCount > 2 && FxTrails.ContainsKey(info.GetArg(2).ToLowerInvariant()) ? info.GetArg(2).ToLowerInvariant() : "pulse";
        var seconds = SecondsArg(info, info.ArgCount > 2 && FxTrails.ContainsKey(info.GetArg(2).ToLowerInvariant()) ? 3 : 2, 30f, 3f, 180f) ?? 30f;
        var off = players.All(p => _auraTimers.ContainsKey(p.SteamID));
        foreach (var t in players)
        {
            if (_auraTimers.Remove(t.SteamID, out var old)) { try { old.Kill(); } catch { } }
            if (off) continue;
            var started = Server.CurrentTime; var steam = t.SteamID; var effect = FxTrails[shape];
            _auraTimers[steam] = AddTimer(0.3f, () =>
            {
                if (!Alive(t) || Server.CurrentTime - started > seconds)
                {
                    if (_auraTimers.Remove(steam, out var done)) { try { done.Kill(); } catch { } }
                    return;
                }
                if (t.PlayerPawn!.Value!.AbsOrigin is not { } o) return;
                SpawnFx(effect, new Vector(o.X, o.Y, o.Z + 36f), RainbowAt((Server.CurrentTime - started) / 6.0), 1.2f);
            }, TimerFlags.REPEAT);
        }
        Announce(off ? "css_aura_off<player>" : "css_aura<player>", off ? "css_aura_off<multiple>" : "css_aura<multiple>", admin, label, off ? "" : seconds.ToString("0"));
        AdminPlus.LogAction($"{admin} {(off ? "took the aura from" : "gave an aura to")} {label}");
    }

    // ---- chance -------------------------------------------------------------------------------------

    private bool RunRoulette(string admin)
    {
        var alive = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsHLTV && Alive(p)).ToList();
        if (alive.Count == 0) return false;
        var victim = alive[FunRandom.Next(alive.Count)];
        var effect = RouletteEffects[FunRandom.Next(RouletteEffects.Length)];
        Server.ExecuteCommand($"css_{effect} #{victim.UserId}");
        return true;
    }

    private void CmdRoulette(CCSPlayerController? caller, CommandInfo info)
    {
        if (!RunRoulette(caller == null ? "Console" : caller.PlayerName)) SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]);
        AdminPlus.LogAction($"{(caller == null ? "Console" : caller.PlayerName)} spun the roulette");
    }

    private void CmdChaos(CCSPlayerController? caller, CommandInfo info)
    {
        var admin = caller == null ? "Console" : caller.PlayerName;
        if (_chaosTimer != null)
        {
            try { _chaosTimer.Kill(); } catch { }
            _chaosTimer = null;
            Announce("css_chaos_off<player>", "css_chaos_off<multiple>", admin, "", "");
            return;
        }
        var seconds = SecondsArg(info, 1, 60f, 10f, 300f) ?? 60f;
        var started = Server.CurrentTime;
        _chaosTimer = AddTimer(5f, () =>
        {
            if (Server.CurrentTime - started > seconds)
            {
                try { _chaosTimer?.Kill(); } catch { }
                _chaosTimer = null;
                return;
            }
            RunRoulette(admin);
        }, TimerFlags.REPEAT);
        Announce("css_chaos<player>", "css_chaos<multiple>", admin, "", seconds.ToString("0"));
        AdminPlus.LogAction($"{admin} started the chaos for {seconds:0} s");
    }
}
