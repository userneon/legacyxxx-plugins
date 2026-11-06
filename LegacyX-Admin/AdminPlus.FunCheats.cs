using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
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
    // Bunny hop (owner report 2026-11-06: the hop was jerky and "fake", no speed gained): setting the player's speed from the server fights the client's own
    // movement and never lets the speed build up. While anyone has css_bhop on, the game's own sv_enablebunnyhopping and sv_autobunnyhopping are switched on
    // (the client predicts them, so it is smooth and the speed is kept). They are server-wide: while any bhop is on, everyone may hold jump to hop. They go back
    // to what they were when the last bhop goes off. If the server does not have those convars the old per-player push is used.
    private static bool bhopByConvar;
    private bool? bhopSavedEnable, bhopSavedAuto;

    private void SyncBhopConvars()
    {
        try
        {
            var enable = ConVar.Find("sv_enablebunnyhopping"); var auto = ConVar.Find("sv_autobunnyhopping");
            if (enable == null || auto == null) { bhopByConvar = false; return; }
            if (bhopSlots.Count > 0 && !bhopByConvar)
            {
                bhopSavedEnable = enable.GetPrimitiveValue<bool>(); bhopSavedAuto = auto.GetPrimitiveValue<bool>();
                Server.ExecuteCommand("sv_enablebunnyhopping 1;sv_autobunnyhopping 1");
                bhopByConvar = true;
            }
            else if (bhopSlots.Count == 0 && bhopByConvar)
            {
                Server.ExecuteCommand($"sv_enablebunnyhopping {(bhopSavedEnable == true ? 1 : 0)};sv_autobunnyhopping {(bhopSavedAuto == true ? 1 : 0)}");
                bhopByConvar = false;
            }
        }
        catch { bhopByConvar = false; }
    }

    private void WatchFunCheatWindow()
    {
        SyncBhopConvars();
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
        // "sv_cheats 1" typed in a root player's own developer console (owner request 2026-11-06). The game engine may keep a client from sending
        // it at all (it is a server-only setting); when it does reach the server it is run here as css_cheats, for root only. Everyone else's goes on as before.
        AddCommandListener("sv_cheats", (caller, info) =>
        {
            if (caller == null || !caller.IsValid || caller.IsBot || !AdminManager.PlayerHasPermissions(caller, "@css/root")) return HookResult.Continue;
            CmdSvCheats(caller, info);
            return HookResult.Handled;
        }, HookMode.Pre);
        AddPermCmd("@css/root", "css_cheats", "[0|1] - sv_cheats on or off (root, any server, any time)", CmdSvCheats);

        RegisterListener<Listeners.OnTick>(OnFunCheatTick);
        RegisterListener<Listeners.CheckTransmit>(OnFunCheatTransmit);
        RegisterListener<Listeners.OnMapStart>(_ => { aimlockTargets.Clear(); wallhackSlots.Clear(); aimlockSlots.Clear(); silentAimSlots.Clear(); bhopSlots.Clear(); wallhackGlows.Clear(); funCheatTimerRunning = false; });
        RegisterListener<Listeners.OnClientDisconnect>(slot => { aimlockTargets.Remove(slot); wallhackSlots.Remove(slot); aimlockSlots.Remove(slot); silentAimSlots.Remove(slot); bhopSlots.Remove(slot); });
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

    /// <summary>
    /// css_cheats [0|1] (owner request 2026-11-06): someone with root really switches sv_cheats, on any server and at any time (no number
    /// toggles). The value is read back a moment later and the answer says what it really is. Off a Fun server and outside warmup everyone is
    /// told in chat, because a round played with cheats on is not an ordinary one; the next warmup end still puts it back to 0.
    /// </summary>
    private void CmdSvCheats(CCSPlayerController? caller, CommandInfo info)
    {
        var current = SvCheatsValue();
        bool want;
        if (info.ArgCount < 2) want = current != true;
        else if (info.GetArg(1) is "0" or "1") want = info.GetArg(1) == "1";
        else { FunReply(caller, "Usage: css_cheats [0|1]"); return; }
        Server.ExecuteCommand($"sv_cheats {(want ? 1 : 0)}");
        var who = caller?.PlayerName ?? "Console";
        AddTimer(0.3f, () =>
        {
            var now = SvCheatsValue();
            if (now == want)
            {
                FunReply(caller, $"sv_cheats is now {(want ? "ON" : "OFF")}.");
                if (want && !CheatsAllowed())
                    foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
                        player.PrintToChat(LegacyXChat.System($"{{white}}{SanitizeName(who)}{{grey}} turned {{white}}sv_cheats{{grey}} on."));
            }
            else FunReply(caller, $"sv_cheats could not be changed: it is {(now == null ? "unknown" : now.Value ? "ON" : "OFF")}.");
            _ = Discord.SendAdminActionLog("sv_cheats", "server", 0UL, who, caller?.SteamID ?? 0UL, want ? "1" : "0", this);
        });
    }

    /// <summary>What sv_cheats is right now (null when it cannot be read).</summary>
    private static bool? SvCheatsValue()
    {
        try { return ConVar.Find("sv_cheats")?.GetPrimitiveValue<bool>(); }
        catch { return null; }
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
        if (aimlockSlots.Count == 0 && (bhopSlots.Count == 0 || bhopByConvar)) return;
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || !player.PawnIsAlive) continue;
            var pawn = player.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) continue;
            if (aimlockSlots.Contains(player.Slot) && (player.Buttons & PlayerButtons.Attack) != 0) AimlockStep(player, pawn);
            else aimlockTargets.Remove(player.Slot);
            // Jump held on the ground: jump again at once, so landing never costs speed.
            if (!bhopByConvar && bhopSlots.Contains(player.Slot) && (player.Buttons & PlayerButtons.Jump) != 0 && (pawn.Flags & 1) != 0)
            {
                var velocity = pawn.AbsVelocity;
                pawn.Teleport(null, null, new Vector(velocity.X, velocity.Y, 301f));
            }
        }
    }

    // Aimlock (owner report 2026-11-06: "it cuts out"): the view used to snap straight onto whoever was nearest in any direction on every tick, so it jumped between
    // enemies and fought the player's own mouse. Now it keeps one target until that enemy dies or leaves a wide cone, takes about a third of the remaining turn each
    // tick (a smooth glide, a few ticks to arrive) and only chooses among enemies in front (90 degrees each side).
    private readonly Dictionary<int, int> aimlockTargets = new();

    private void AimlockStep(CCSPlayerController player, CCSPlayerPawn pawn)
    {
        CCSPlayerPawn? enemy = null; Vector? head = null;
        if (aimlockTargets.TryGetValue(player.Slot, out var lockedSlot) && Utilities.GetPlayerFromSlot(lockedSlot) is { IsValid: true, PawnIsAlive: true } locked && locked.TeamNum != player.TeamNum && locked.PlayerPawn?.Value is { IsValid: true, AbsOrigin: not null } lockedPawn)
        {
            var from = new Vector(pawn.AbsOrigin!.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z + 64f);
            var to = new Vector(lockedPawn.AbsOrigin!.X, lockedPawn.AbsOrigin.Y, lockedPawn.AbsOrigin.Z + 58f);
            var yaw = MathF.Atan2(to.Y - from.Y, to.X - from.X) * 180f / MathF.PI;
            if (MathF.Abs(((yaw - pawn.EyeAngles.Y + 540f) % 360f) - 180f) <= 120f) { enemy = lockedPawn; head = to; }
        }
        if (enemy == null)
        {
            enemy = NearestEnemy(player, pawn, 90f, out head);
            if (enemy == null || head == null) { aimlockTargets.Remove(player.Slot); return; }
            var owner = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.PlayerPawn?.Value is { } pp && pp.Index == enemy.Index);
            if (owner != null) aimlockTargets[player.Slot] = owner.Slot;
        }
        var eye = new Vector(pawn.AbsOrigin!.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z + 64f);
        var dx = head!.X - eye.X; var dy = head.Y - eye.Y; var dz = head.Z - eye.Z;
        var flat = MathF.Sqrt(dx * dx + dy * dy);
        var wantPitch = -MathF.Atan2(dz, flat) * 180f / MathF.PI;
        var wantYaw = MathF.Atan2(dy, dx) * 180f / MathF.PI;
        var current = pawn.EyeAngles;
        var deltaYaw = ((wantYaw - current.Y + 540f) % 360f) - 180f;
        var deltaPitch = wantPitch - current.X;
        const float glide = 0.35f;
        pawn.Teleport(null, new QAngle(current.X + deltaPitch * glide, current.Y + deltaYaw * glide, 0f), null);
    }

    /// <summary>
    /// Silent aim (owner report 2026-11-06: "it works wrongly"): it used to hit with one fixed damage whatever the weapon, from a 25-degree cone at any range. Now the damage
    /// is the weapon's own, the cone is 12 degrees, the range 2500 units, some shots are headshots and the shooter is told "Hit" or "Headshot"; a knife or grenade is never helped.
    /// </summary>
    private static float SilentAimDamage(string weapon)
    {
        weapon = weapon ?? "";
        if (weapon.Contains("awp")) return 115f;
        if (weapon.Contains("ssg08")) return 88f;
        if (weapon.Contains("scar20") || weapon.Contains("g3sg1")) return 80f;
        if (weapon.Contains("deagle")) return 53f;
        if (weapon.Contains("ak47")) return 36f;
        if (weapon.Contains("nova") || weapon.Contains("xm1014") || weapon.Contains("mag7") || weapon.Contains("sawedoff")) return 60f;
        if (weapon.Contains("m249") || weapon.Contains("negev")) return 32f;
        if (weapon.Contains("knife") || weapon.Contains("grenade") || weapon.Contains("flashbang") || weapon.Contains("molotov") || weapon.Contains("incgrenade") || weapon.Contains("decoy") || weapon.Contains("c4") || weapon.Contains("taser")) return 0f;
        return weapon.Contains("mp") || weapon.Contains("ump") || weapon.Contains("p90") || weapon.Contains("bizon") || weapon.Contains("mac10") ? 26f : weapon.Contains("glock") || weapon.Contains("usp") || weapon.Contains("p250") || weapon.Contains("tec9") || weapon.Contains("fiveseven") || weapon.Contains("elite") || weapon.Contains("hkp2000") || weapon.Contains("cz75") ? 28f : 33f;
    }

    /// <summary>
    /// A thin bright line from the shooter to the enemy for a moment, so the shot is seen going to them (owner request 2026-11-06: "the bullet goes straight at the enemy").
    /// Where the player's real bullet flies cannot be changed from the server (CounterStrikeSharp 1.0.342 has no hook on the player's input), so this line and the damage are what
    /// a silent aim shows. Never fails the shot.
    /// </summary>
    private void DrawShotBeam(Vector from, Vector to)
    {
        try
        {
            var beam = Utilities.CreateEntityByName<CBeam>("beam");
            if (beam == null || !beam.IsValid) return;
            beam.Render = Color.FromArgb(255, 255, 80, 60);
            beam.Width = 1.5f;
            beam.Teleport(from, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            beam.EndPos.X = to.X; beam.EndPos.Y = to.Y; beam.EndPos.Z = to.Z;
            beam.DispatchSpawn();
            Utilities.SetStateChanged(beam, "CBeam", "m_vecEndPos");
            AddTimer(0.15f, () => { if (beam.IsValid) beam.Remove(); });
        }
        catch (Exception exception) { LogError($"shot line: {exception.Message}"); }
    }

    private HookResult OnFunCheatWeaponFire(EventWeaponFire @event, GameEventInfo eventInfo)
    {
        var shooter = @event.Userid;
        if (shooter == null || !shooter.IsValid || !silentAimSlots.Contains(shooter.Slot) || !shooter.PawnIsAlive) return HookResult.Continue;
        var pawn = shooter.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null) return HookResult.Continue;
        var enemy = NearestEnemy(shooter, pawn, 12f, out var aim);
        if (enemy == null || aim == null) return HookResult.Continue;
        var weapon = @event.Weapon ?? "";
        var damage = SilentAimDamage(weapon);
        if (damage <= 0f) return HookResult.Continue;
        var from = pawn.AbsOrigin;
        var distance = MathF.Sqrt((aim.X - from.X) * (aim.X - from.X) + (aim.Y - from.Y) * (aim.Y - from.Y) + (aim.Z - from.Z) * (aim.Z - from.Z));
        if (distance > 2500f) return HookResult.Continue;
        // A fixed hit on the enemy nearest the line of fire. The game's own damage call needs a native damage record this CounterStrikeSharp cannot build, so the health is
        // cut directly: the damage is the weapon's own (see SilentAimDamage), a quarter of the shots are headshots (double), and the kill is a plain death (no kill feed
        // credit for the shooter).
        DrawShotBeam(new Vector(from.X, from.Y, from.Z + 60f), aim);
        var headshot = FunRandom.NextDouble() < 0.25;
        var dealt = (int)MathF.Round(damage * (headshot ? 2f : 1f));
        enemy.Health -= dealt;
        Utilities.SetStateChanged(enemy, "CBaseEntity", "m_iHealth");
        if (enemy.Health <= 0) enemy.CommitSuicide(false, true);
        else shooter.PrintToCenter(headshot ? "Headshot" : "Hit");
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
                if (!allowed) { info.TransmitEntities.Remove((int)glow.Index); continue; }
                // The game sends only what the viewer could see (its visibility set), so an enemy far behind walls or across the map came and went: the glow showed
                // "sometimes" (owner report 2026-11-06). The glow and the player it follows are sent to a wallhack viewer on every update.
                info.TransmitEntities.Add((int)pawnIndex);
                info.TransmitEntities.Add((int)glow.Index);
            }
        }
    }

    private static bool OwnerIsEnemy(CCSPlayerController viewer, uint pawnIndex)
    {
        var owner = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.PlayerPawn?.Value is { } pawn && pawn.Index == pawnIndex);
        return owner != null && owner.TeamNum != viewer.TeamNum;
    }
}
