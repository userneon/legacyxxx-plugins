using System.Numerics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using LegacyX.Shared.Configuration;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
#if RAYTRACE
using CounterStrikeSharp.API.Core.Capabilities;
using RayTraceAPI;
#endif

namespace LegacyXAntiWallhack;

/// <summary>
/// Anti-wallhack by the server (owner request 2026-11-06): an enemy who is fully behind solid cover is not sent to the viewer's game at all, so a
/// wallhack has nothing to draw. The game server decides, with line-of-sight traces from the viewer's eye to several points on and around the enemy
/// (and a ball of smoke), and removes the enemy's player entity and weapons from what it transmits to that viewer (CheckTransmit).
///
/// It needs the Ray-Trace module (Metamod module RayTrace plus the CounterStrikeSharp plugin RayTraceImpl, github.com/FUNPLAY-pro-CS2/Ray-Trace)
/// on the server; without it, or without LEGACYX_ANTIWALLHACK_ENABLED=true, this plugin does nothing. It is built to fail open: whenever it is not sure
/// (no tracer, a trace that does not answer, too many traces in one update, warmup, a dead viewer, a close enemy) the enemy is sent as usual.
///
/// Settings (central .env, per server with LEGACYX_&lt;port&gt;_ANTIWALLHACK_...): ENABLED (false), MODES (competitive_5v5,proleague), UPDATE_TICKS (2),
/// GRACE_TICKS (20), PADDING (28 units), MIN_DISTANCE (140 units), SMOKE (true), SMOKE_RADIUS (125 units), PREDICT_MS (100), TRACE_BUDGET (900 per update), DEBUG (false).
/// </summary>
public sealed class LegacyXAntiWallhack : BasePlugin
{
    public override string ModuleAuthor => "LEGACY-X";
    public override string ModuleName => "LEGACY-X Anti-wallhack";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    private bool active;
    private bool debug;
    private int updateTicks, graceTicks, traceBudget;
    private float padding, minDistance, smokeRadius, predictSeconds;
    private bool smokeOcclusion;

    /// <summary>viewer slot → enemy slot → what the viewer has seen of them.</summary>
    private readonly Dictionary<int, Dictionary<int, AntiWallhackLogic.PairState>> pairs = new();
    /// <summary>viewer slot → entity indexes not sent to that viewer (the enemies' players and weapons). Replaced as a whole each update.</summary>
    private Dictionary<int, HashSet<int>> hidden = new();
    private readonly Dictionary<int, (Vector3 Center, float Since)> smokes = new();
    private readonly List<Vector3> samples = new();

    private long statUpdates, statTraces, statBudgetHits, statHiddenNow;
    private bool warnedNoTracer;
#if RAYTRACE
    private static PluginCapability<CRayTraceInterface> RayTraceCapability { get; } = new("raytrace:craytraceinterface");
    private CRayTraceInterface? tracer;
#endif

    public override void Load(bool hotReload)
    {
        var environment = LegacyXEnvironmentLoader.Load();
        if (!environment.GetModuleBoolean("ANTIWALLHACK", "ENABLED", false))
        {
            Console.WriteLine($"[{ModuleName}] Off (LEGACYX_ANTIWALLHACK_ENABLED is not true).");
            return;
        }
#if !RAYTRACE
        Console.WriteLine($"[{ModuleName}] Off: this build has no RayTraceApi.dll (run scripts/build-raytrace.sh, then build again).");
        return;
#else
        var mode = LegacyXServerRuntime.Load(environment).ServerMode;
        var modes = environment.GetModule("ANTIWALLHACK", "MODES", "competitive_5v5,proleague");
        if (!AntiWallhackLogic.ModeMatches(mode, modes))
        {
            Console.WriteLine($"[{ModuleName}] Off on a '{mode}' server (it runs on: {modes}).");
            return;
        }
        updateTicks = environment.GetModuleInt("ANTIWALLHACK", "UPDATE_TICKS", 2, 1, 16);
        graceTicks = environment.GetModuleInt("ANTIWALLHACK", "GRACE_TICKS", 20, 0, 256);
        padding = environment.GetModuleInt("ANTIWALLHACK", "PADDING", 28, 0, 128);
        minDistance = environment.GetModuleInt("ANTIWALLHACK", "MIN_DISTANCE", 140, 0, 1000);
        smokeOcclusion = environment.GetModuleBoolean("ANTIWALLHACK", "SMOKE", true);
        smokeRadius = environment.GetModuleInt("ANTIWALLHACK", "SMOKE_RADIUS", 125, 40, 300);
        predictSeconds = environment.GetModuleInt("ANTIWALLHACK", "PREDICT_MS", 100, 0, 400) / 1000f;
        traceBudget = environment.GetModuleInt("ANTIWALLHACK", "TRACE_BUDGET", 900, 50, 5000);
        debug = environment.GetModuleBoolean("ANTIWALLHACK", "DEBUG", false);
        active = true;

        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.CheckTransmit>(OnCheckTransmit);
        RegisterListener<Listeners.OnMapStart>(_ => Reset());
        RegisterListener<Listeners.OnClientDisconnect>(slot => Forget(slot));
        RegisterEventHandler<EventSmokegrenadeDetonate>((@event, _) => { smokes[@event.Entityid] = (new Vector3(@event.X, @event.Y, @event.Z), Server.CurrentTime); return HookResult.Continue; });
        RegisterEventHandler<EventSmokegrenadeExpired>((@event, _) => { smokes.Remove(@event.Entityid); return HookResult.Continue; });
        RegisterEventHandler<EventRoundStart>((_, _) => { Reset(); return HookResult.Continue; });
        Console.WriteLine($"[{ModuleName}] On: {mode}, every {updateTicks} tick(s), grace {graceTicks} ticks, padding {padding} units, budget {traceBudget} traces. lx_awh_status shows what it does.");
#endif
    }

    private void Reset()
    {
        pairs.Clear();
        hidden = new Dictionary<int, HashSet<int>>();
        smokes.Clear();
    }

    private void Forget(int slot)
    {
        pairs.Remove(slot);
        foreach (var byEnemy in pairs.Values) byEnemy.Remove(slot);
        var next = new Dictionary<int, HashSet<int>>(hidden);
        next.Remove(slot);
        hidden = next;
    }

    [ConsoleCommand("lx_awh_status", "Anti-wallhack: what it has done so far (server console)")]
    public void OnStatus(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null) return;
        command.ReplyToCommand($"[{ModuleName}] {(active ? "on" : "off")}; updates {statUpdates}, traces {statTraces}, budget reached {statBudgetHits}x, enemies hidden from someone now: {statHiddenNow}, smokes known: {smokes.Count}.");
    }

#if RAYTRACE
    private void OnTick()
    {
        if (!active) return;
        var tick = Server.TickCount;
        if (tick % updateTicks != 0) return;
        try
        {
            Update(tick);
        }
        catch (Exception exception)
        {
            // Never let a failure here hide anyone: everything is shown until the next good update.
            hidden = new Dictionary<int, HashSet<int>>();
            if (debug) Console.WriteLine($"[{ModuleName}] Update failed, nothing is hidden: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static bool InWarmup()
    {
        try { return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules?.WarmupPeriod == true; }
        catch { return false; }
    }

    private void Update(int tick)
    {
        tracer ??= RayTraceCapability.Get();
        if (tracer == null)
        {
            if (!warnedNoTracer) { warnedNoTracer = true; Console.WriteLine($"[{ModuleName}] The ray trace module is not loaded (RayTraceImpl): nothing is hidden."); }
            hidden = new Dictionary<int, HashSet<int>>();
            return;
        }
        if (InWarmup())
        {
            hidden = new Dictionary<int, HashSet<int>>();
            return;
        }
        statUpdates++;
        var now = Server.CurrentTime;
        foreach (var expired in smokes.Where(entry => now - entry.Value.Since > 22f).Select(entry => entry.Key).ToList()) smokes.Remove(expired);

        var everyone = Utilities.GetPlayers().Where(player => player.IsValid && player.PawnIsAlive && player.TeamNum >= 2 && player.PlayerPawn?.Value is { IsValid: true, AbsOrigin: not null }).ToList();
        var next = new Dictionary<int, HashSet<int>>();
        var traces = 0;
        long hiddenPairs = 0;
        foreach (var viewer in everyone)
        {
            // Bots do not cheat, and the GOTV and spectators have to see everything.
            if (viewer.IsBot || viewer.IsHLTV) continue;
            var viewerPawn = viewer.PlayerPawn!.Value!;
            if (!pairs.TryGetValue(viewer.Slot, out var states)) pairs[viewer.Slot] = states = new();
            var eye = ToVector3(viewerPawn.AbsOrigin!) + new Vector3(0, 0, (viewerPawn.Flags & 2) != 0 ? 46f : 64f);
            var viewerVelocity = ToVector3(viewerPawn.AbsVelocity);
            foreach (var enemy in everyone)
            {
                if (enemy.TeamNum == viewer.TeamNum || enemy.Slot == viewer.Slot) continue;
                var enemyPawn = enemy.PlayerPawn!.Value!;
                if (!states.TryGetValue(enemy.Slot, out var state)) states[enemy.Slot] = state = new AntiWallhackLogic.PairState { LastVisibleTick = tick };
                var seen = true;   // not sure → shown
                if (traces < traceBudget)
                {
                    seen = IsSeen(viewerPawn, eye, viewerVelocity, enemyPawn, ref traces);
                }
                else statBudgetHits++;
                if (!AntiWallhackLogic.Update(state, seen, tick, graceTicks)) continue;
                hiddenPairs++;
                if (!next.TryGetValue(viewer.Slot, out var set)) next[viewer.Slot] = set = new HashSet<int>();
                set.Add((int)enemyPawn.Index);
                var weapons = enemyPawn.WeaponServices?.MyWeapons;
                if (weapons != null)
                    foreach (var handle in weapons)
                        if (handle.Value is { IsValid: true } weapon) set.Add((int)weapon.Index);
            }
        }
        statTraces += traces;
        statHiddenNow = hiddenPairs;
        hidden = next;
        if (debug && tick % 640 == 0) Console.WriteLine($"[{ModuleName}] {everyone.Count} players, {traces} traces this update, {hiddenPairs} enemies hidden from someone.");
    }

    /// <summary>
    /// True when the viewer can see any of the points round the enemy, now or a moment from now (both of them moving on for PREDICT_MS), or when
    /// the enemy is close. A trace that fails to answer counts as clear.
    /// </summary>
    private bool IsSeen(CCSPlayerPawn viewerPawn, Vector3 eye, Vector3 viewerVelocity, CCSPlayerPawn enemyPawn, ref int traces)
    {
        var enemyOrigin = ToVector3(enemyPawn.AbsOrigin!);
        if (Vector3.Distance(eye, enemyOrigin) < minDistance) return true;
        var ducking = (enemyPawn.Flags & 2) != 0;
        var enemyVelocity = ToVector3(enemyPawn.AbsVelocity);
        // The view of now, then the view a few ticks ahead (the viewer and the enemy both carry on), which shows a peek before it happens.
        for (var step = 0; step < 2; step++)
        {
            if (step == 1 && predictSeconds <= 0f) break;
            var at = step == 0 ? eye : eye + viewerVelocity * predictSeconds;
            var target = step == 0 ? enemyOrigin : enemyOrigin + enemyVelocity * predictSeconds;
            AntiWallhackLogic.SamplePoints(target, ducking, padding, samples);
            foreach (var point in samples)
            {
                if (smokeOcclusion && InSmoke(at, point)) continue;
                traces++;
                if (!Blocked(viewerPawn, at, point)) return true;
            }
        }
        return false;
    }

    private bool InSmoke(Vector3 from, Vector3 to)
    {
        foreach (var (center, since) in smokes.Values)
            if (Server.CurrentTime - since > 1.2f && AntiWallhackLogic.SegmentCrossesSphere(from, to, center, smokeRadius)) return true;
        return false;
    }

    /// <summary>One line-of-sight trace: world solids only (glass, bullet-pass things such as fences, clip brushes and triggers do not block the eye).</summary>
    private bool Blocked(CCSPlayerPawn viewerPawn, Vector3 from, Vector3 to)
    {
        var options = new TraceOptions(InteractionLayers.Solid, InteractionLayers.Window | InteractionLayers.PassBullets | InteractionLayers.PlayerClip | InteractionLayers.Trigger);
        try
        {
            return tracer!.TraceEndShape(new Vector(from.X, from.Y, from.Z), new Vector(to.X, to.Y, to.Z), viewerPawn, options, out var result) && result.DidHit && result.Fraction < 0.99f;
        }
        catch
        {
            return false;
        }
    }

    private static Vector3 ToVector3(Vector vector) => new(vector.X, vector.Y, vector.Z);

    private void OnCheckTransmit(CCheckTransmitInfoList infoList)
    {
        if (!active || hidden.Count == 0) return;
        var current = hidden;
        foreach (var (info, viewer) in infoList)
        {
            if (viewer == null || !viewer.IsValid || !current.TryGetValue(viewer.Slot, out var set)) continue;
            foreach (var index in set) info.TransmitEntities.Remove(index);
        }
    }
#else
    private void OnTick() { }
    private void OnCheckTransmit(CCheckTransmitInfoList infoList) { }
#endif

    public override void Unload(bool hotReload)
    {
        active = false;
        Reset();
    }
}
