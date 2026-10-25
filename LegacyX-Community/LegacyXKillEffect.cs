using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace LegacyXCommunity;

/// <summary>
/// Kill effects bought on legacyx.cc (Shop → Kill effects, owner request 2026-10-25): where the player's victim falls, a short effect in
/// the worn colour plays, with a flash of light. <c>kill_effect</c> is the #rrggbb, <c>kill_effect_fx</c> the shape: ring (spreads over
/// the floor), pillar (a beam up into the sky), burst (rays outward), spiral (rises round the body), lightning (a bolt from above),
/// rainbow (a ring cycling through colours). Pillar and lightning stay below wall height (130–140 units). Over the victim's head (owner request 2026-10-25): halo, crown, cross, xmark (an X),
/// heart (these three face the killer) and soul (a light rising out of the body into the sky). It is gone within a second, never plays for a team kill or a suicide, and changes nothing
/// about the kill. It is built from the same short beams as the grenade trail; lx_kill_effect 0 switches it off on a server.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private static readonly HashSet<string> KillShapes = new() { "ring", "pillar", "burst", "spiral", "lightning", "rainbow", "halo", "crown", "cross", "xmark", "heart", "soul" };
    private readonly Dictionary<ulong, (System.Drawing.Color Color, string Shape)> killLooks = new();
    private bool killEffectEnabled = true;

    private void RegisterKillEffects()
    {
        RegisterEventHandler<EventPlayerDeath>((@event, _) =>
        {
            if (!killEffectEnabled) return HookResult.Continue;
            var attacker = @event.Attacker;
            var victim = @event.Userid;
            if (attacker == null || victim == null || !attacker.IsValid || !victim.IsValid || attacker == victim) return HookResult.Continue;
            if (attacker.TeamNum == victim.TeamNum) return HookResult.Continue;
            if (!killLooks.TryGetValue(attacker.SteamID, out var look)) return HookResult.Continue;
            if (victim.PlayerPawn.Value?.AbsOrigin is not { } at) return HookResult.Continue;
            var from = attacker.PlayerPawn.Value?.AbsOrigin;
            PlayKillEffect(new Vector(at.X, at.Y, at.Z), from is { } seen ? new Vector(seen.X, seen.Y, seen.Z) : null, look);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            if (@event.Userid is { } left) killLooks.Remove(left.SteamID);
            return HookResult.Continue;
        });
        AddCommand("lx_kill_effect", "Kill effects on this server: lx_kill_effect 0|1", (player, command) =>
        {
            if (player != null) return; // server console / RCON only
            if (command.ArgCount > 1 && command.ArgByIndex(1) is "0" or "1") killEffectEnabled = command.ArgByIndex(1) == "1";
            command.ReplyToCommand($"[{ModuleName}] lx_kill_effect is {(killEffectEnabled ? 1 : 0)}.");
        });
    }

    private void PlayKillEffect(Vector at, Vector? killer, (System.Drawing.Color Color, string Shape) look)
    {
        var color = look.Color;
        Vector P(float x, float y, float z) => new(at.X + x, at.Y + y, at.Z + z);
        // Flat shapes over the head stand upright and face the killer: `side` runs across their view.
        var (sideX, sideY) = killer is { } k ? Side(at, k) : (1f, 0f);
        const float head = 78f;
        Vector F(float across, float up) => new(at.X + sideX * across, at.Y + sideY * across, at.Z + head + up);
        void Lines(float width, float seconds, params (float A, float U)[] points)
        {
            for (var i = 1; i < points.Length; i++) SpawnTrailSegment(F(points[i - 1].A, points[i - 1].U), F(points[i].A, points[i].U), color, width, seconds);
        }
        // A ring of short beams on the floor, `radius` wide; `hue` colours each beam on its own (rainbow).
        void Ring(float radius, float z, float width, float seconds, Func<int, System.Drawing.Color>? hue = null)
        {
            const int sides = 16;
            for (var i = 0; i < sides; i++)
            {
                var a = i * MathF.Tau / sides;
                var b = (i + 1) * MathF.Tau / sides;
                SpawnTrailSegment(P(MathF.Cos(a) * radius, MathF.Sin(a) * radius, z), P(MathF.Cos(b) * radius, MathF.Sin(b) * radius, z), hue?.Invoke(i) ?? color, width, seconds);
            }
        }
        switch (look.Shape)
        {
            case "pillar":
                // Kept below a wall's height, so it never shows over cover where a kill happened.
                SpawnTrailSegment(P(0, 0, 0), P(0, 0, 130), color, 12f, 0.7f);
                SpawnTrailSegment(P(0, 0, 0), P(0, 0, 130), System.Drawing.Color.White, 3f, 0.7f);
                Ring(18, 2, 3f, 0.7f);
                break;
            case "burst":
                for (var i = 0; i < 10; i++)
                {
                    var a = i * MathF.Tau / 10;
                    var up = i % 2 == 0 ? 30f : 50f;
                    SpawnTrailSegment(P(MathF.Cos(a) * 8, MathF.Sin(a) * 8, 30), P(MathF.Cos(a) * 70, MathF.Sin(a) * 70, up), color, 3f, 0.5f);
                }
                break;
            case "spiral":
            {
                Vector? last = null;
                for (var i = 0; i <= 28; i++)
                {
                    var a = i * 0.55f;
                    var next = P(MathF.Cos(a) * 26, MathF.Sin(a) * 26, i * 4.5f);
                    if (last != null) SpawnTrailSegment(last, next, color, 3f, 0.8f);
                    last = next;
                }
                break;
            }
            case "lightning":
            {
                var random = new Random();
                // From just over head height, not the sky: a bolt over cover would show where a kill happened.
                var from = P(0, 0, 140);
                for (var i = 1; i <= 7; i++)
                {
                    var to = i == 7 ? P(0, 0, 0) : P(random.Next(-22, 23), random.Next(-22, 23), 140 - i * 20);
                    SpawnTrailSegment(from, to, color, 5f, 0.35f);
                    SpawnTrailSegment(from, to, System.Drawing.Color.White, 1.5f, 0.35f);
                    from = to;
                }
                Ring(30, 2, 3f, 0.35f);
                AddTimer(0.15f, () => FlashLight(P(0, 0, 60), color, 0.2f));
                break;
            }
            case "rainbow":
                Ring(24, 2, 4f, 0.3f, i => Hue(i * 360 / 16));
                AddTimer(0.15f, () => Ring(48, 2, 3.5f, 0.3f, i => Hue((i * 360 / 16 + 60) % 360)));
                AddTimer(0.3f, () => Ring(72, 2, 3f, 0.3f, i => Hue((i * 360 / 16 + 120) % 360)));
                break;
            case "halo":
                Ring(13, head + 10, 3.5f, 1f);
                Ring(13, head + 10, 1.2f, 1f, _ => System.Drawing.Color.White);
                break;
            case "crown":
            {
                const int points = 10;
                for (var i = 0; i < points; i++)
                {
                    float A(int n) => n * MathF.Tau / points;
                    float U(int n) => n % 2 == 0 ? 12f : 0f;
                    SpawnTrailSegment(P(MathF.Cos(A(i)) * 12, MathF.Sin(A(i)) * 12, head + 4 + U(i)), P(MathF.Cos(A(i + 1)) * 12, MathF.Sin(A(i + 1)) * 12, head + 4 + U(i + 1)), color, 3f, 1f);
                }
                Ring(12, head + 4, 3f, 1f);
                break;
            }
            case "cross":
                Lines(4f, 1f, (0, 4), (0, 40));
                Lines(4f, 1f, (-11, 29), (11, 29));
                break;
            case "xmark":
                Lines(4f, 0.9f, (-12, 6), (12, 30));
                Lines(4f, 0.9f, (-12, 30), (12, 6));
                break;
            case "heart":
            {
                var shape = new (float A, float U)[25];
                for (var i = 0; i < shape.Length; i++)
                {
                    var t = i * MathF.Tau / (shape.Length - 1);
                    shape[i] = (16 * MathF.Pow(MathF.Sin(t), 3) * 0.9f, (13 * MathF.Cos(t) - 5 * MathF.Cos(2 * t) - 2 * MathF.Cos(3 * t) - MathF.Cos(4 * t)) * 0.9f + 22);
                }
                Lines(3.5f, 1f, shape);
                break;
            }
            case "soul":
            {
                // A light rising out of the body, leaving a fading line behind it.
                var light = SpawnGrenadeLight(P(0, 0, 40), color);
                var last = P(0, 0, 40);
                var step = 0;
                CounterStrikeSharp.API.Modules.Timers.Timer? timer = null;
                timer = AddTimer(0.05f, () =>
                {
                    step++;
                    var next = P(MathF.Sin(step * 0.6f) * 6, MathF.Cos(step * 0.6f) * 6, 40 + step * 9);
                    SpawnTrailSegment(last, next, color, 5f, 0.5f);
                    if (light != null && light.IsValid) light.Teleport(next, new QAngle(0, 0, 0), new Vector(0, 0, 0));
                    last = next;
                    if (step >= 20) { timer?.Kill(); if (light != null && light.IsValid) light.Remove(); }
                }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
                return;
            }
            default: // ring
                Ring(24, 2, 4f, 0.3f);
                AddTimer(0.15f, () => Ring(48, 2, 3.5f, 0.3f));
                AddTimer(0.3f, () => Ring(72, 2, 3f, 0.3f));
                break;
        }
        var overHead = look.Shape is "halo" or "crown" or "cross" or "xmark" or "heart";
        FlashLight(P(0, 0, overHead ? head + 20 : 40), look.Shape == "rainbow" ? System.Drawing.Color.White : color, overHead ? 0.8f : 0.4f);
    }

    /// <summary>A short burst of coloured light that lights the floor and walls around the body.</summary>
    private void FlashLight(Vector at, System.Drawing.Color color, float seconds)
    {
        try
        {
            var light = Utilities.CreateEntityByName<CBarnLight>("light_omni2");
            if (light == null || !light.IsValid) return;
            light.Enabled = true;
            light.Color = color;
            light.Brightness = 6f;
            light.Range = 260f;
            light.Teleport(at, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            light.DispatchSpawn();
            AddTimer(seconds, () => { if (light.IsValid) light.Remove(); });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Kill effect light failed: {exception.Message}");
        }
    }

    /// <summary>Keeps (or drops) a player's worn kill effect; only a plain #rrggbb counts (rainbow needs none). Main thread.</summary>
    private void StoreKillEffect(ulong steamId, string? hex, string? shape)
    {
        var kind = shape is not null && KillShapes.Contains(shape) ? shape : "ring";
        if (ParseSmokeColor(hex) is { } rgb) killLooks[steamId] = (System.Drawing.Color.FromArgb(255, (int)rgb.R, (int)rgb.G, (int)rgb.B), kind);
        else if (kind == "rainbow") killLooks[steamId] = (System.Drawing.Color.White, kind);
        else killLooks.Remove(steamId);
    }
}
