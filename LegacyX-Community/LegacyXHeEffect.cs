using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace LegacyXCommunity;

/// <summary>
/// HE grenade explosion effects bought on legacyx.cc (Shop → Grenades → Explosion, owner request 2026-10-25): where the player's HE
/// grenade explodes, a short effect in the worn colour plays on top of the game's own blast, with a wide flash of coloured light.
/// <c>he_effect</c> is the #rrggbb, <c>he_effect_fx</c> the shape: shockwave (a ring rushing out along the floor), dome (rings rising
/// round the blast like a half sphere), burst (rays out and up), embers (glowing embers thrown up and falling), prism (rays in every
/// colour). It lasts about half a second; the blast itself, its damage and its sound stay the game's own. lx_he_effect 0 switches it off
/// on a server.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private static readonly HashSet<string> HeShapes = new() { "shockwave", "dome", "burst", "embers", "prism" };
    private readonly Dictionary<ulong, (System.Drawing.Color Color, string Shape)> heLooks = new();
    private bool heEffectEnabled = true;

    private void RegisterHeEffects()
    {
        RegisterEventHandler<EventHegrenadeDetonate>((@event, _) =>
        {
            if (!heEffectEnabled || @event.Userid is not { IsValid: true } thrower) return HookResult.Continue;
            if (heLooks.TryGetValue(thrower.SteamID, out var look)) PlayHeEffect(new Vector(@event.X, @event.Y, @event.Z), look);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            if (@event.Userid is { } left) heLooks.Remove(left.SteamID);
            return HookResult.Continue;
        });
        AddCommand("lx_he_effect", "HE explosion effects on this server: lx_he_effect 0|1", (player, command) =>
        {
            if (player != null) return; // server console / RCON only
            if (command.ArgCount > 1 && command.ArgByIndex(1) is "0" or "1") heEffectEnabled = command.ArgByIndex(1) == "1";
            command.ReplyToCommand($"[{ModuleName}] lx_he_effect is {(heEffectEnabled ? 1 : 0)}.");
        });
    }

    private void PlayHeEffect(Vector at, (System.Drawing.Color Color, string Shape) look)
    {
        var color = look.Color;
        Vector P(float x, float y, float z) => new(at.X + x, at.Y + y, at.Z + z);
        // A flat ring round the blast at height `z`.
        void Ring(float radius, float z, float width, float seconds)
        {
            const int sides = 18;
            for (var i = 0; i < sides; i++)
            {
                var a = i * MathF.Tau / sides;
                var b = (i + 1) * MathF.Tau / sides;
                SpawnTrailSegment(P(MathF.Cos(a) * radius, MathF.Sin(a) * radius, z), P(MathF.Cos(b) * radius, MathF.Sin(b) * radius, z), color, width, seconds);
            }
        }
        // Rays out and up (never down into the floor); `hue` colours each ray on its own (prism).
        void Rays(int count, float length, float width, float seconds, Func<int, System.Drawing.Color>? hue = null)
        {
            for (var i = 0; i < count; i++)
            {
                var a = i * MathF.Tau / count;
                var up = 0.25f + (i % 3) * 0.3f;
                var flat = MathF.Sqrt(1f - up * up);
                SpawnTrailSegment(P(0, 0, 6), P(MathF.Cos(a) * flat * length, MathF.Sin(a) * flat * length, 6 + up * length), hue?.Invoke(i) ?? color, width, seconds);
            }
        }
        switch (look.Shape)
        {
            case "dome":
                Ring(60, 4, 4f, 0.25f);
                AddTimer(0.08f, () => Ring(52, 30, 3.5f, 0.25f));
                AddTimer(0.16f, () => Ring(36, 52, 3f, 0.25f));
                AddTimer(0.24f, () => Ring(16, 62, 2.5f, 0.25f));
                break;
            case "burst":
                Rays(16, 110, 4f, 0.4f);
                Rays(8, 60, 1.6f, 0.4f, _ => System.Drawing.Color.White);
                break;
            case "embers":
                for (var i = 0; i < 18; i++)
                {
                    var a = Random.Shared.NextSingle() * MathF.Tau;
                    var reach = 30f + Random.Shared.NextSingle() * 70f;
                    var top = P(MathF.Cos(a) * reach * 0.6f, MathF.Sin(a) * reach * 0.6f, 40f + Random.Shared.NextSingle() * 60f);
                    var fall = P(MathF.Cos(a) * reach, MathF.Sin(a) * reach, 8f);
                    SpawnTrailSegment(P(0, 0, 8), top, color, 2.5f, 0.35f);
                    var shade = i % 3 == 0 ? System.Drawing.Color.White : color; // decided now: the loop variable has moved on by the time the timer runs
                    AddTimer(0.2f, () => SpawnTrailSegment(top, fall, shade, 2f, 0.5f));
                }
                break;
            case "prism":
                Rays(18, 110, 3.5f, 0.45f, i => Hue(i * 360 / 18));
                Ring(50, 4, 3f, 0.3f);
                break;
            default: // shockwave
                Ring(40, 4, 6f, 0.2f);
                AddTimer(0.1f, () => Ring(90, 4, 5f, 0.2f));
                AddTimer(0.2f, () => Ring(140, 4, 4f, 0.22f));
                break;
        }
        FlashLight(P(0, 0, 30), look.Shape == "prism" ? System.Drawing.Color.White : color, 0.45f);
    }

    /// <summary>Keeps (or drops) a player's worn HE explosion effect; only a plain #rrggbb counts (prism needs none). Main thread.</summary>
    private void StoreHeEffect(ulong steamId, string? hex, string? shape)
    {
        var kind = shape is not null && HeShapes.Contains(shape) ? shape : "shockwave";
        if (ParseSmokeColor(hex) is { } rgb) heLooks[steamId] = (System.Drawing.Color.FromArgb(255, (int)rgb.R, (int)rgb.G, (int)rgb.B), kind);
        else if (kind == "prism") heLooks[steamId] = (System.Drawing.Color.White, kind);
        else heLooks.Remove(steamId);
    }
}
