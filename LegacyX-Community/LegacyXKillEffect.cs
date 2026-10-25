using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace LegacyXCommunity;

/// <summary>
/// Kill effects bought on legacyx.cc (Shop → Kill effects, owner request 2026-10-25): where the player's victim falls, a short effect in
/// the worn colour plays, with a flash of light. <c>kill_effect</c> is the #rrggbb, <c>kill_effect_fx</c> the shape: ring (spreads over
/// the floor), pillar (a beam up into the sky), burst (rays outward), spiral (rises round the body), lightning (a bolt from above),
/// rainbow (a ring cycling through colours). It is gone within a second, never plays for a team kill or a suicide, and changes nothing
/// about the kill. It is built from the same short beams as the grenade trail; lx_kill_effect 0 switches it off on a server.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private static readonly HashSet<string> KillShapes = new() { "ring", "pillar", "burst", "spiral", "lightning", "rainbow" };
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
            PlayKillEffect(new Vector(at.X, at.Y, at.Z), look);
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

    private void PlayKillEffect(Vector at, (System.Drawing.Color Color, string Shape) look)
    {
        var color = look.Color;
        Vector P(float x, float y, float z) => new(at.X + x, at.Y + y, at.Z + z);
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
                SpawnTrailSegment(P(0, 0, 0), P(0, 0, 420), color, 12f, 0.7f);
                SpawnTrailSegment(P(0, 0, 0), P(0, 0, 420), System.Drawing.Color.White, 3f, 0.7f);
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
                var from = P(0, 0, 480);
                for (var i = 1; i <= 8; i++)
                {
                    var to = i == 8 ? P(0, 0, 0) : P(random.Next(-40, 41), random.Next(-40, 41), 480 - i * 60);
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
            default: // ring
                Ring(24, 2, 4f, 0.3f);
                AddTimer(0.15f, () => Ring(48, 2, 3.5f, 0.3f));
                AddTimer(0.3f, () => Ring(72, 2, 3f, 0.3f));
                break;
        }
        FlashLight(P(0, 0, 40), look.Shape == "rainbow" ? System.Drawing.Color.White : color, 0.4f);
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
