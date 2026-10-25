using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace LegacyXCommunity;

/// <summary>
/// Flash effects bought on legacyx.cc (Shop → Grenades → Flash, owner request 2026-10-25): where the player's flashbang bursts, a short
/// effect in the worn colour plays with a flash of coloured light. <c>flash_effect</c> is the #rrggbb, <c>flash_effect_fx</c> the shape:
/// starburst (rays out in every direction), nova (three rings spreading round the burst like a sphere), ring (a shockwave along the
/// floor), prism (rays in every colour). It lasts under half a second and changes nothing about the flash itself: who is blinded and
/// for how long stays the game's own. lx_flash_effect 0 switches it off on a server.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private static readonly HashSet<string> FlashShapes = new() { "starburst", "nova", "ring", "prism" };
    private readonly Dictionary<ulong, (System.Drawing.Color Color, string Shape)> flashLooks = new();
    private bool flashEffectEnabled = true;

    private void RegisterFlashEffects()
    {
        RegisterEventHandler<EventFlashbangDetonate>((@event, _) =>
        {
            if (!flashEffectEnabled || @event.Userid is not { IsValid: true } thrower) return HookResult.Continue;
            if (flashLooks.TryGetValue(thrower.SteamID, out var look)) PlayFlashEffect(new Vector(@event.X, @event.Y, @event.Z), look);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            if (@event.Userid is { } left) flashLooks.Remove(left.SteamID);
            return HookResult.Continue;
        });
        AddCommand("lx_flash_effect", "Flash effects on this server: lx_flash_effect 0|1", (player, command) =>
        {
            if (player != null) return; // server console / RCON only
            if (command.ArgCount > 1 && command.ArgByIndex(1) is "0" or "1") flashEffectEnabled = command.ArgByIndex(1) == "1";
            command.ReplyToCommand($"[{ModuleName}] lx_flash_effect is {(flashEffectEnabled ? 1 : 0)}.");
        });
    }

    private void PlayFlashEffect(Vector at, (System.Drawing.Color Color, string Shape) look)
    {
        var color = look.Color;
        Vector P(float x, float y, float z) => new(at.X + x, at.Y + y, at.Z + z);
        // Rays out in every direction (spread evenly over a sphere); `hue` colours each ray on its own (prism).
        void Rays(int count, float length, float width, float seconds, Func<int, System.Drawing.Color>? hue = null)
        {
            var golden = MathF.PI * (3f - MathF.Sqrt(5f));
            for (var i = 0; i < count; i++)
            {
                var y = 1f - (i + 0.5f) * 2f / count;
                var r = MathF.Sqrt(1f - y * y);
                var a = golden * i;
                var (dx, dy, dz) = (MathF.Cos(a) * r, MathF.Sin(a) * r, y);
                SpawnTrailSegment(P(dx * 8, dy * 8, dz * 8), P(dx * length, dy * length, dz * length), hue?.Invoke(i) ?? color, width, seconds);
            }
        }
        // A ring round the burst in one of three planes (0 = floor, 1 = upright along x, 2 = upright along y).
        void Ring(float radius, int plane, float width, float seconds)
        {
            const int sides = 16;
            Vector On(float angle)
            {
                var (c, s) = (MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
                return plane switch { 0 => P(c, s, 0), 1 => P(c, 0, s), _ => P(0, c, s) };
            }
            for (var i = 0; i < sides; i++) SpawnTrailSegment(On(i * MathF.Tau / sides), On((i + 1) * MathF.Tau / sides), color, width, seconds);
        }
        switch (look.Shape)
        {
            case "nova":
                for (var plane = 0; plane < 3; plane++) Ring(22, plane, 3f, 0.2f);
                AddTimer(0.12f, () => { for (var plane = 0; plane < 3; plane++) Ring(46, plane, 2.4f, 0.22f); });
                break;
            case "ring":
                Ring(30, 0, 4f, 0.2f);
                AddTimer(0.12f, () => Ring(60, 0, 3.5f, 0.2f));
                AddTimer(0.24f, () => Ring(90, 0, 3f, 0.2f));
                break;
            case "prism":
                Rays(16, 80, 3f, 0.35f, i => Hue(i * 360 / 16));
                break;
            default: // starburst
                Rays(14, 75, 3f, 0.35f);
                Rays(6, 40, 1.5f, 0.35f, _ => System.Drawing.Color.White);
                break;
        }
        FlashLight(at, look.Shape == "prism" ? System.Drawing.Color.White : color, 0.35f);
    }

    /// <summary>Keeps (or drops) a player's worn flash effect; only a plain #rrggbb counts (prism needs none). Main thread.</summary>
    private void StoreFlashEffect(ulong steamId, string? hex, string? shape)
    {
        var kind = shape is not null && FlashShapes.Contains(shape) ? shape : "starburst";
        if (ParseSmokeColor(hex) is { } rgb) flashLooks[steamId] = (System.Drawing.Color.FromArgb(255, (int)rgb.R, (int)rgb.G, (int)rgb.B), kind);
        else if (kind == "prism") flashLooks[steamId] = (System.Drawing.Color.White, kind);
        else flashLooks.Remove(steamId);
    }
}
