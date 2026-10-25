using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace LegacyXCommunity;

/// <summary>
/// The Grenade trail cosmetic (owner request 2026-10-25): a thin line in the colour the player bought and wears on legacyx.cc follows
/// every grenade they throw (HE, flash, smoke, molotov, incendiary, decoy) while it flies. The colour comes with the profile
/// (<c>grenade_trail</c>, #rrggbb), read with the smoke colour. The line is short beam segments from where the grenade was to where it
/// is, each fading away after <see cref="TrailSegmentSeconds"/>; it stops when the grenade lands or after a few seconds. Nothing about
/// the grenade itself changes. lx_grenade_trail 0 switches it off on a server.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private static readonly HashSet<string> GrenadeProjectiles = new() { "hegrenade_projectile", "flashbang_projectile", "smokegrenade_projectile", "molotov_projectile", "incgrenade_projectile", "decoy_projectile" };
    private readonly Dictionary<ulong, System.Drawing.Color> trailColors = new();
    private const float TrailTickSeconds = 0.05f;
    private const float TrailSegmentSeconds = 1.0f;
    private const int TrailMaxTicks = 120; // 6 s at most per grenade
    private bool trailEnabled = true;

    private void RegisterGrenadeTrail()
    {
        RegisterListener<Listeners.OnEntitySpawned>(entity =>
        {
            if (!trailEnabled || !GrenadeProjectiles.Contains(entity.DesignerName)) return;
            var projectile = new CBaseCSGrenadeProjectile(entity.Handle);
            Server.NextFrame(() =>
            {
                if (!projectile.IsValid) return;
                var thrower = projectile.Thrower.Value;
                if (thrower == null || !thrower.IsValid) return;
                var controller = new CCSPlayerPawn(thrower.Handle).Controller.Value;
                if (controller == null || !controller.IsValid || !trailColors.TryGetValue(controller.SteamID, out var color)) return;
                FollowGrenade(projectile, color);
            });
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            if (@event.Userid is { } left) trailColors.Remove(left.SteamID);
            return HookResult.Continue;
        });
        AddCommand("lx_grenade_trail", "Grenade trails on this server: lx_grenade_trail 0|1", (player, command) =>
        {
            if (player != null) return; // server console / RCON only
            if (command.ArgCount > 1 && command.ArgByIndex(1) is "0" or "1") trailEnabled = command.ArgByIndex(1) == "1";
            command.ReplyToCommand($"[{ModuleName}] lx_grenade_trail is {(trailEnabled ? 1 : 0)}.");
        });
    }

    private void FollowGrenade(CBaseCSGrenadeProjectile projectile, System.Drawing.Color color)
    {
        if (projectile.AbsOrigin is not { } origin) return;
        var last = new Vector(origin.X, origin.Y, origin.Z);
        var ticks = 0;
        var still = 0;
        CounterStrikeSharp.API.Modules.Timers.Timer? timer = null;
        timer = AddTimer(TrailTickSeconds, () =>
        {
            ticks++;
            if (!projectile.IsValid || projectile.AbsOrigin is not { } now || ticks > TrailMaxTicks) { timer?.Kill(); return; }
            var moved = Math.Abs(now.X - last.X) + Math.Abs(now.Y - last.Y) + Math.Abs(now.Z - last.Z);
            // Resting on the ground: a few still ticks and the trail is done.
            if (moved < 1f) { if (++still > 4) timer?.Kill(); return; }
            still = 0;
            var to = new Vector(now.X, now.Y, now.Z);
            SpawnTrailSegment(last, to, color);
            last = to;
        }, TimerFlags.REPEAT);
    }

    private void SpawnTrailSegment(Vector from, Vector to, System.Drawing.Color color)
    {
        try
        {
            var beam = Utilities.CreateEntityByName<CBeam>("beam");
            if (beam == null || !beam.IsValid) return;
            beam.Render = color;
            beam.Width = 1.6f;
            beam.Teleport(from, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            beam.EndPos.X = to.X;
            beam.EndPos.Y = to.Y;
            beam.EndPos.Z = to.Z;
            beam.DispatchSpawn();
            AddTimer(TrailSegmentSeconds, () => { if (beam.IsValid) beam.Remove(); });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Grenade trail failed: {exception.Message}");
        }
    }

    /// <summary>Keeps (or drops) a player's worn grenade trail colour; only a plain #rrggbb counts. Main thread.</summary>
    private void StoreGrenadeTrail(ulong steamId, string? hex)
    {
        if (ParseSmokeColor(hex) is { } rgb) trailColors[steamId] = System.Drawing.Color.FromArgb(255, (int)rgb.R, (int)rgb.G, (int)rgb.B);
        else trailColors.Remove(steamId);
    }
}
