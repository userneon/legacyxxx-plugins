using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using LegacyX.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AdminPlus;

/// <summary>
/// Cheat toys for Fun servers (owner request 2026-10-27), owner only (@css/root): css_wh (see enemies through walls), css_aimlock (view
/// snaps to the nearest enemy while the fire button is held), css_silentaim (shots hurt the nearest enemy in front, the view does not move),
/// css_bhop (jump held = automatic bunny hop) and css_cheats (sv_cheats). Each takes a player or <c>all</c> and 1/0 (no number toggles).
/// On a Fun server they work all the time; on every other mode (owner request 2026-10-30, "all modes") they work only while the server is in
/// warmup, so a round that counts is never played with them: when the warmup ends every switch is cleared and sv_cheats is put back to 0.
/// Each use is written to the admin log like the other fun commands. A switch lasts until the map changes or the player leaves.
/// </summary>
public partial class AdminPlus
{
    private readonly HashSet<int> wallhackSlots = new(), aimlockSlots = new(), silentAimSlots = new(), bhopSlots = new();
    // One glowing copy of each player's model, drawn only for the players who switched the wallhack on (see CheckTransmit below).
    private readonly Dictionary<uint, CDynamicProp> wallhackGlows = new();
    private bool funCheatTimerRunning;

    private static readonly Lazy<bool> funServer = new(() =>
        LegacyXServerRuntime.Load(LegacyXEnvironmentLoader.Load()).ServerMode.StartsWith("fun", StringComparison.OrdinalIgnoreCase));

    private static bool IsFunServer() => funServer.Value;

    private static bool InWarmup()
    {
        try { return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules?.WarmupPeriod == true; }
        catch { return false; }
    }

    /// <summary>Fun servers: always. Any other mode: only while the server is in warmup.</summary>
    private static bool CheatsAllowed() => IsFunServer() || InWarmup();

    private bool cheatsWereOpen;

    /// <summary>Once a second: when the warmup of a non-Fun server ends, every cheat switch is cleared and sv_cheats goes back to 0.</summary>
    private void WatchFunCheatWindow()
    {
        var open = CheatsAllowed();
        if (!open && cheatsWereOpen)
        {
            var any = wallhackSlots.Count + aimlockSlots.Count + silentAimSlots.Count + bhopSlots.Count > 0;
            wallhackSlots.Clear(); aimlockSlots.Clear(); silentAimSlots.Clear(); bhopSlots.Clear();
            ClearWallhackGlows();
            Server.ExecuteCommand("sv_cheats 0");
            if (any) Console.WriteLine("[AdminPlus] The warmup ended: the cheat switches are off and sv_cheats is 0.");
        }
        cheatsWereOpen = open;
    }

    private void RegisterFunCheatCommands()
    {
        AddPermCmd("@css/root", "css_wh", "<#userid|name|all> [0|1] - See enemies through walls", (caller, info) => FunCheatSwitch(caller, info, "wh", wallhackSlots));
        AddPermCmd("@css/root", "css_aimlock", "<#userid|name|all> [0|1] - View snaps to the nearest enemy while firing", (caller, info) => FunCheatSwitch(caller, info, "aimlock", aimlockSlots));
        AddPermCmd("@css/root", "css_silentaim", "<#userid|name|all> [0|1] - Shots hurt the nearest enemy in front", (caller, info) => FunCheatSwitch(caller, info, "silentaim", silentAimSlots));
        AddPermCmd("@css/root", "css_bhop", "<#userid|name|all> [0|1] - Hold jump to bunny hop", (caller, info) => FunCheatSwitch(caller, info, "bhop", bhopSlots));
        AddPermCmd("@css/root", "css_cheats", "<0|1> - sv_cheats on or off", CmdSvCheats);

        RegisterListener<Listeners.OnTick>(OnFunCheatTick);
        RegisterListener<Listeners.CheckTransmit>(OnFunCheatTransmit);
        RegisterListener<Listeners.OnMapStart>(_ => { wallhackSlots.Clear(); aimlockSlots.Clear(); silentAimSlots.Clear(); bhopSlots.Clear(); wallhackGlows.Clear(); funCheatTimerRunning = false; });
        RegisterListener<Listeners.OnClientDisconnect>(slot => { wallhackSlots.Remove(slot); aimlockSlots.Remove(slot); silentAimSlots.Remove(slot); bhopSlots.Remove(slot); });
        RegisterEventHandler<EventWeaponFire>(OnFunCheatWeaponFire);
        AddTimer(1f, WatchFunCheatWindow, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
    }

    private static void FunReply(CCSPlayerController? caller, string text)
    {
        if (caller != null && caller.IsValid) caller.Print(text); else Console.WriteLine(text);
    }

    private void FunCheatSwitch(CCSPlayerController? caller, CommandInfo info, string name, HashSet<int> slots)
    {
        if (info.ArgCount < 2) { FunReply(caller, $"Usage: css_{(name == "wh" ? "wh" : name)} <player|all> [0|1]"); return; }
        if (!CheatsAllowed()) { FunReply(caller, "These work on Fun servers, and on other modes only during warmup."); return; }
        if (!ResolveTargets(caller, info.GetArg(1), false, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }
        int asked = -1;
        if (info.ArgCount >= 3 && !int.TryParse(info.GetArg(2), out asked)) { FunReply(caller, "The last value must be 0 or 1."); return; }
        foreach (var target in players)
        {
            var on = asked switch { 0 => false, 1 => true, _ => !slots.Contains(target.Slot) };
            if (on) slots.Add(target.Slot); else slots.Remove(target.Slot);
        }
        var state = asked switch { 0 => "off", 1 => "on", _ => "toggled" };
        FunReply(caller, $"{name}: {state} for {label}.");
        if (name == "wh") EnsureFunCheatTimer();
        AddTimer(0.1f, () =>
        {
            foreach (var target in players)
                _ = Discord.SendAdminActionLog(name, target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, state, this);
        });
    }

    private void CmdSvCheats(CCSPlayerController? caller, CommandInfo info)
    {
        if (!CheatsAllowed()) { FunReply(caller, "sv_cheats can be turned on at Fun servers, and on other modes only during warmup."); return; }
        if (info.ArgCount < 2 || info.GetArg(1) is not ("0" or "1")) { FunReply(caller, "Usage: css_cheats <0|1>"); return; }
        Server.ExecuteCommand($"sv_cheats {info.GetArg(1)}");
        FunReply(caller, $"sv_cheats {(info.GetArg(1) == "1" ? "on" : "off")}.");
        AddTimer(0.1f, () => _ = Discord.SendAdminActionLog("sv_cheats", "server", 0UL, caller?.PlayerName ?? "Console", caller?.SteamID ?? 0UL, info.GetArg(1), this));
    }

    /// <summary>The nearest living enemy of <paramref name="viewer"/> in front of or around them, with the point on it to aim at.</summary>
    private static CCSPlayerPawn? NearestEnemy(CCSPlayerController viewer, CCSPlayerPawn own, float maxAngle, out Vector? aimAt)
    {
        aimAt = null;
        CCSPlayerPawn? best = null; var bestDistance = float.MaxValue;
        var eye = new Vector(own.AbsOrigin!.X, own.AbsOrigin.Y, own.AbsOrigin.Z + 64f);
        foreach (var other in Utilities.GetPlayers())
        {
            if (!other.IsValid || other.Slot == viewer.Slot || other.TeamNum == viewer.TeamNum || !other.PawnIsAlive) continue;
            var pawn = other.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) continue;
            var head = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z + 58f);
            var dx = head.X - eye.X; var dy = head.Y - eye.Y; var dz = head.Z - eye.Z;
            var distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (distance >= bestDistance) continue;
            if (maxAngle < 180f)
            {
                var yaw = MathF.Atan2(dy, dx) * 180f / MathF.PI;
                var diff = MathF.Abs(((yaw - own.EyeAngles.Y + 540f) % 360f) - 180f);
                if (diff > maxAngle) continue;
            }
            best = pawn; bestDistance = distance; aimAt = head;
        }
        return best;
    }

    private void OnFunCheatTick()
    {
        if (aimlockSlots.Count == 0 && bhopSlots.Count == 0) return;
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || !player.PawnIsAlive) continue;
            var pawn = player.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) continue;
            if (aimlockSlots.Contains(player.Slot) && (player.Buttons & PlayerButtons.Attack) != 0)
            {
                var enemy = NearestEnemy(player, pawn, 180f, out var head);
                if (enemy != null && head != null)
                {
                    var eye = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z + 64f);
                    var dx = head.X - eye.X; var dy = head.Y - eye.Y; var dz = head.Z - eye.Z;
                    var flat = MathF.Sqrt(dx * dx + dy * dy);
                    var angles = new QAngle(-MathF.Atan2(dz, flat) * 180f / MathF.PI, MathF.Atan2(dy, dx) * 180f / MathF.PI, 0f);
                    pawn.Teleport(null, angles, null);
                }
            }
            // Jump held on the ground: jump again at once, so landing never costs speed.
            if (bhopSlots.Contains(player.Slot) && (player.Buttons & PlayerButtons.Jump) != 0 && (pawn.Flags & 1) != 0)
            {
                var velocity = pawn.AbsVelocity;
                pawn.Teleport(null, null, new Vector(velocity.X, velocity.Y, 301f));
            }
        }
    }

    private HookResult OnFunCheatWeaponFire(EventWeaponFire @event, GameEventInfo eventInfo)
    {
        var shooter = @event.Userid;
        if (shooter == null || !shooter.IsValid || !silentAimSlots.Contains(shooter.Slot) || !shooter.PawnIsAlive) return HookResult.Continue;
        var pawn = shooter.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) return HookResult.Continue;
        var enemy = NearestEnemy(shooter, pawn, 25f, out _);
        if (enemy == null) return HookResult.Continue;
        var weapon = @event.Weapon ?? "";
        var damage = weapon.Contains("awp") || weapon.Contains("ssg08") || weapon.Contains("scar20") || weapon.Contains("g3sg1") ? 100 : weapon.Contains("knife") || weapon.Contains("grenade") || weapon.Contains("flashbang") || weapon.Contains("molotov") || weapon.Contains("decoy") || weapon.Contains("c4") ? 0 : 40;
        if (damage == 0) return HookResult.Continue;
        enemy.Health -= damage;
        Utilities.SetStateChanged(enemy, "CBaseEntity", "m_iHealth");
        if (enemy.Health <= 0) enemy.CommitSuicide(false, true);
        return HookResult.Continue;
    }

    // ---- Wallhack: a glowing copy of each living player's model that only the wallhack players are sent ----

    private void EnsureFunCheatTimer()
    {
        if (funCheatTimerRunning) return;
        funCheatTimerRunning = true;
        AddTimer(0.5f, () =>
        {
            if (wallhackSlots.Count == 0) { ClearWallhackGlows(); funCheatTimerRunning = false; return; }
            RefreshWallhackGlows();
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT | CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void ClearWallhackGlows()
    {
        foreach (var glow in wallhackGlows.Values) if (glow.IsValid) glow.Remove();
        wallhackGlows.Clear();
    }

    private void RefreshWallhackGlows()
    {
        var alive = new HashSet<uint>();
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || !player.PawnIsAlive) continue;
            var pawn = player.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid) continue;
            alive.Add(pawn.Index);
            if (wallhackGlows.TryGetValue(pawn.Index, out var existing) && existing.IsValid) continue;
            var model = pawn.CBodyComponent?.SceneNode?.GetSkeletonInstance()?.ModelState.ModelName;
            if (string.IsNullOrEmpty(model)) continue;
            var glow = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            if (glow == null || !glow.IsValid) continue;
            glow.SetModel(model);
            glow.Spawnflags = 256;
            glow.Render = Color.FromArgb(1, 255, 255, 255);
            glow.DispatchSpawn();
            glow.Glow.GlowColorOverride = player.Team == CsTeam.Terrorist ? Color.FromArgb(255, 255, 140, 0) : Color.FromArgb(255, 70, 160, 255);
            glow.Glow.GlowRange = 5000;
            glow.Glow.GlowType = 3;
            glow.Glow.GlowTeam = -1;
            glow.AcceptInput("FollowEntity", pawn, glow, "!activator");
            wallhackGlows[pawn.Index] = glow;
        }
        foreach (var gone in wallhackGlows.Keys.Where(index => !alive.Contains(index)).ToList())
        {
            if (wallhackGlows[gone].IsValid) wallhackGlows[gone].Remove();
            wallhackGlows.Remove(gone);
        }
    }

    // Sent only to a player who switched the wallhack on, and only for the other team's players: everyone else never receives the copy.
    private void OnFunCheatTransmit(CCheckTransmitInfoList infoList)
    {
        if (wallhackGlows.Count == 0) return;
        foreach (var (info, viewer) in infoList)
        {
            foreach (var (pawnIndex, glow) in wallhackGlows)
            {
                if (!glow.IsValid) continue;
                var allowed = viewer != null && viewer.IsValid && wallhackSlots.Contains(viewer.Slot) && OwnerIsEnemy(viewer, pawnIndex);
                if (!allowed) info.TransmitEntities.Remove((int)glow.Index);
            }
        }
    }

    private static bool OwnerIsEnemy(CCSPlayerController viewer, uint pawnIndex)
    {
        var owner = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.PlayerPawn?.Value is { } pawn && pawn.Index == pawnIndex);
        return owner != null && owner.TeamNum != viewer.TeamNum;
    }
}
