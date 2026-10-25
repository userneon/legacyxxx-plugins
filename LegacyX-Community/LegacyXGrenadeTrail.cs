using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace LegacyXCommunity;

/// <summary>
/// Grenade cosmetics bought on legacyx.cc (Shop → Grenades), read from the player profile with the smoke colour:
/// <list type="bullet">
/// <item><b>Grenade trail</b> (owner request 2026-10-25): a line in the worn colour follows every grenade the player throws (HE,
/// flash, smoke, molotov, incendiary, decoy). <c>grenade_trail</c> is the #rrggbb, <c>grenade_trail_fx</c> the shape: line (thick),
/// dash, double, helix, comet, rainbow (cycles through colours, ignores the colour), zigzag (two lines zigzagging to either side and
/// crossing on the way), and with effects along the way (owner request
/// 2026-10-25): flare (drops burning flares that light the ground they pass), sparks (sparks fly off the line), starfall (twinkling
/// stars scatter round it) and inferno (all three on a thick line).</item>
/// <item><b>Grenade light</b> (sold separately): a coloured light that flies with the grenade and lights the walls it passes
/// (<c>grenade_light</c>, #rrggbb).</item>
/// </list>
/// The line is short beam segments from where the grenade was to where it is, each removed after a moment; everything stops when the
/// grenade lands or after a few seconds. Nothing about the grenade itself changes. lx_grenade_trail 0 switches both off on a server.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private static readonly HashSet<string> GrenadeProjectiles = new() { "hegrenade_projectile", "flashbang_projectile", "smokegrenade_projectile", "molotov_projectile", "incgrenade_projectile", "decoy_projectile" };
    private static readonly HashSet<string> TrailShapes = new() { "line", "dash", "double", "helix", "comet", "rainbow", "zigzag", "flare", "sparks", "starfall", "inferno" };
    private readonly Dictionary<ulong, (System.Drawing.Color Color, string Shape)> trailLooks = new();
    private readonly Dictionary<ulong, System.Drawing.Color> grenadeLights = new();
    private const float TrailTickSeconds = 0.04f;
    private const int TrailMaxTicks = 150; // 6 s at most per grenade
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
                if (controller == null || !controller.IsValid) return;
                var hasTrail = trailLooks.TryGetValue(controller.SteamID, out var look);
                var hasLight = grenadeLights.TryGetValue(controller.SteamID, out var light);
                if (hasTrail || hasLight) FollowGrenade(projectile, hasTrail ? look : null, hasLight ? light : null);
            });
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            if (@event.Userid is { } left) { trailLooks.Remove(left.SteamID); grenadeLights.Remove(left.SteamID); }
            return HookResult.Continue;
        });
        AddCommand("lx_grenade_trail", "Grenade trails and lights on this server: lx_grenade_trail 0|1", (player, command) =>
        {
            if (player != null) return; // server console / RCON only
            if (command.ArgCount > 1 && command.ArgByIndex(1) is "0" or "1") trailEnabled = command.ArgByIndex(1) == "1";
            command.ReplyToCommand($"[{ModuleName}] lx_grenade_trail is {(trailEnabled ? 1 : 0)}.");
        });
    }

    private void FollowGrenade(CBaseCSGrenadeProjectile projectile, (System.Drawing.Color Color, string Shape)? trail, System.Drawing.Color? lightColor)
    {
        if (projectile.AbsOrigin is not { } origin) return;
        var last = new Vector(origin.X, origin.Y, origin.Z);
        var lastLeft = last;
        var lastRight = last;
        var ticks = 0;
        var still = 0;
        var light = lightColor is { } shade ? SpawnGrenadeLight(last, shade) : null;
        CounterStrikeSharp.API.Modules.Timers.Timer? timer = null;
        void Stop()
        {
            timer?.Kill();
            if (light != null && light.IsValid) light.Remove();
        }
        timer = AddTimer(TrailTickSeconds, () =>
        {
            ticks++;
            if (!projectile.IsValid || projectile.AbsOrigin is not { } now || ticks > TrailMaxTicks) { Stop(); return; }
            var to = new Vector(now.X, now.Y, now.Z);
            if (light != null && light.IsValid) light.Teleport(to, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            var moved = Math.Abs(to.X - last.X) + Math.Abs(to.Y - last.Y) + Math.Abs(to.Z - last.Z);
            // Resting on the ground: a few still ticks and the trail (and light) are done.
            if (moved < 1f) { if (++still > 5) Stop(); return; }
            still = 0;
            if (trail is { } look) DrawTrail(look, ticks, last, to, ref lastLeft, ref lastRight);
            last = to;
        }, TimerFlags.REPEAT);
    }

    private void DrawTrail((System.Drawing.Color Color, string Shape) look, int tick, Vector from, Vector to, ref Vector lastLeft, ref Vector lastRight)
    {
        var color = look.Color;
        switch (look.Shape)
        {
            case "dash":
                if (tick % 3 != 0) SpawnTrailSegment(from, to, color, 5f, 1.3f);
                break;
            case "double":
            case "helix":
            {
                // Two strands beside the flight path: side by side (double) or turning round it (helix).
                var (sx, sy) = Side(from, to);
                var turn = look.Shape == "helix" ? tick * 0.7 : 0;
                var r = 7f;
                var left = new Vector(to.X + sx * r * (float)Math.Cos(turn), to.Y + sy * r * (float)Math.Cos(turn), to.Z + r * (float)Math.Sin(turn));
                var right = new Vector(to.X - sx * r * (float)Math.Cos(turn), to.Y - sy * r * (float)Math.Cos(turn), to.Z - r * (float)Math.Sin(turn));
                if (tick > 1) { SpawnTrailSegment(lastLeft, left, color, 3.2f, 1.3f); SpawnTrailSegment(lastRight, right, color, 3.2f, 1.3f); }
                lastLeft = left;
                lastRight = right;
                break;
            }
            case "zigzag":
            {
                // Two lines that zigzag to either side of the flight, mirrored, so they cross every few ticks.
                var (sx, sy) = Side(from, to);
                var phase = tick % 6 / 3f; // 0..2
                var wave = 1f - Math.Abs(phase - 1f) * 2f; // -1..1, a sharp triangle
                const float reach = 10f;
                var left = new Vector(to.X + sx * reach * wave, to.Y + sy * reach * wave, to.Z);
                var right = new Vector(to.X - sx * reach * wave, to.Y - sy * reach * wave, to.Z);
                if (tick > 1) { SpawnTrailSegment(lastLeft, left, color, 3.5f, 1.3f); SpawnTrailSegment(lastRight, right, color, 3.5f, 1.3f); }
                lastLeft = left;
                lastRight = right;
                break;
            }
            case "comet":
                SpawnTrailSegment(from, to, color, 10f, 0.5f);
                break;
            case "rainbow":
                SpawnTrailSegment(from, to, Hue(tick * 14 % 360), 6f, 1.4f);
                break;
            case "flare":
                SpawnTrailSegment(from, to, color, 5f, 1.3f);
                if (tick % 5 == 0) { SpawnFlare(to, color, 1.1f); Twinkle(to, System.Drawing.Color.White, 5f, 1.1f); }
                break;
            case "sparks":
                SpawnTrailSegment(from, to, color, 5f, 1.3f);
                if (tick % 2 == 0) { Spark(to, color); Spark(to, System.Drawing.Color.White); }
                break;
            case "starfall":
                SpawnTrailSegment(from, to, color, 4f, 1.3f);
                if (tick % 3 == 0) Twinkle(Scatter(to, 22f), tick % 6 == 0 ? System.Drawing.Color.White : color, 4f + Random.Shared.NextSingle() * 4f, 0.7f);
                break;
            case "inferno":
                SpawnTrailSegment(from, to, color, 8f, 0.9f);
                SpawnTrailSegment(from, to, System.Drawing.Color.White, 2f, 0.6f);
                if (tick % 2 == 0) Spark(to, color);
                if (tick % 3 == 0) Twinkle(Scatter(to, 18f), color, 5f, 0.6f);
                if (tick % 6 == 0) SpawnFlare(to, color, 0.9f);
                break;
            default:
                SpawnTrailSegment(from, to, color, 6f, 1.4f);
                break;
        }
    }

    /// <summary>A point a little way off `at` in a random direction (up to `radius` units).</summary>
    private static Vector Scatter(Vector at, float radius)
    {
        float Next() => (Random.Shared.NextSingle() * 2f - 1f) * radius;
        return new Vector(at.X + Next(), at.Y + Next(), at.Z + Next());
    }

    /// <summary>A spark flying off the line: a short thin beam outward in a random direction, gone in a moment.</summary>
    private void Spark(Vector at, System.Drawing.Color color)
    {
        var end = Scatter(at, 20f);
        SpawnTrailSegment(at, new Vector(end.X, end.Y, end.Z - 6f), color, 1.5f, 0.3f);
    }

    /// <summary>A twinkling star: three short beams crossing at `at`.</summary>
    private void Twinkle(Vector at, System.Drawing.Color color, float size, float seconds)
    {
        SpawnTrailSegment(new Vector(at.X - size, at.Y, at.Z), new Vector(at.X + size, at.Y, at.Z), color, 1.4f, seconds);
        SpawnTrailSegment(new Vector(at.X, at.Y - size, at.Z), new Vector(at.X, at.Y + size, at.Z), color, 1.4f, seconds);
        SpawnTrailSegment(new Vector(at.X, at.Y, at.Z - size), new Vector(at.X, at.Y, at.Z + size), color, 1.4f, seconds);
    }

    /// <summary>A burning flare left behind in the air: a small light that lights the ground and walls near it for a moment.</summary>
    private void SpawnFlare(Vector at, System.Drawing.Color color, float seconds)
    {
        try
        {
            var light = Utilities.CreateEntityByName<CBarnLight>("light_omni2");
            if (light == null || !light.IsValid) return;
            light.Enabled = true;
            light.Color = color;
            light.Brightness = 3f;
            light.Range = 140f;
            light.Teleport(at, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            light.DispatchSpawn();
            AddTimer(seconds, () => { if (light.IsValid) light.Remove(); });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Grenade flare failed: {exception.Message}");
        }
    }

    /// <summary>A horizontal unit vector at right angles to the flight (for strands beside it).</summary>
    private static (float X, float Y) Side(Vector from, Vector to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        return length < 0.01f ? (1f, 0f) : (-dy / length, dx / length);
    }

    private static System.Drawing.Color Hue(int degrees)
    {
        var h = degrees / 60.0;
        var x = 1 - Math.Abs(h % 2 - 1);
        var (r, g, b) = h switch { < 1 => (1.0, x, 0.0), < 2 => (x, 1.0, 0.0), < 3 => (0.0, 1.0, x), < 4 => (0.0, x, 1.0), < 5 => (x, 0.0, 1.0), _ => (1.0, 0.0, x) };
        return System.Drawing.Color.FromArgb(255, (int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    private void SpawnTrailSegment(Vector from, Vector to, System.Drawing.Color color, float width, float seconds)
    {
        try
        {
            var beam = Utilities.CreateEntityByName<CBeam>("beam");
            if (beam == null || !beam.IsValid) return;
            beam.Render = color;
            beam.Width = width;
            beam.Teleport(from, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            beam.EndPos.X = to.X;
            beam.EndPos.Y = to.Y;
            beam.EndPos.Z = to.Z;
            beam.DispatchSpawn();
            AddTimer(seconds, () => { if (beam.IsValid) beam.Remove(); });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Grenade trail failed: {exception.Message}");
        }
    }

    private CBarnLight? SpawnGrenadeLight(Vector at, System.Drawing.Color color)
    {
        try
        {
            var light = Utilities.CreateEntityByName<CBarnLight>("light_omni2");
            if (light == null || !light.IsValid) return null;
            light.Enabled = true;
            light.Color = color;
            light.Brightness = 4f;
            light.Range = 220f;
            light.Teleport(at, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            light.DispatchSpawn();
            // Never left behind, whatever happens to the grenade.
            AddTimer(TrailMaxTicks * TrailTickSeconds + 1f, () => { if (light.IsValid) light.Remove(); });
            return light;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Grenade light failed: {exception.Message}");
            return null;
        }
    }

    /// <summary>Keeps (or drops) a player's worn grenade trail (colour and shape) and grenade light; only a plain #rrggbb counts. Main thread.</summary>
    private void StoreGrenadeLooks(ulong steamId, string? trailHex, string? trailShape, string? lightHex)
    {
        var shape = trailShape is not null && TrailShapes.Contains(trailShape) ? trailShape : "line";
        if (ParseSmokeColor(trailHex) is { } rgb) trailLooks[steamId] = (System.Drawing.Color.FromArgb(255, (int)rgb.R, (int)rgb.G, (int)rgb.B), shape);
        else if (shape == "rainbow") trailLooks[steamId] = (System.Drawing.Color.White, shape);
        else trailLooks.Remove(steamId);
        if (ParseSmokeColor(lightHex) is { } glow) grenadeLights[steamId] = System.Drawing.Color.FromArgb(255, (int)glow.R, (int)glow.G, (int)glow.B);
        else grenadeLights.Remove(steamId);
    }
}
