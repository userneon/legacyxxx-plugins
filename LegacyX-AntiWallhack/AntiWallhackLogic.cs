using System;
using System.Collections.Generic;
using System.Numerics;

namespace LegacyXAntiWallhack;

/// <summary>
/// The parts of the anti-wallhack that need no game: where an enemy is looked for, whether a puff of smoke is in the way, and when an enemy
/// is hidden. Kept apart so they can be read, and tested, on their own.
/// </summary>
internal static class AntiWallhackLogic
{
    /// <summary>A standing player is about 72 units tall and 32 wide; a crouching one about 54.</summary>
    public const float HalfWidth = 16f;

    /// <summary>
    /// The points on (and around) an enemy that the viewer's eye tries to see: the middle of the body at three heights, and the four corners of a box
    /// around it that is <paramref name="padding"/> units wider than the player, at two heights. The padding is why an enemy is shown a moment before
    /// they step into view: the game server is a few ticks behind what the viewer's screen shows, and a player who peeks must already be there.
    /// </summary>
    public static void SamplePoints(Vector3 origin, bool ducking, float padding, List<Vector3> into)
    {
        into.Clear();
        float low = 8f, middle = ducking ? 28f : 36f, high = ducking ? 48f : 60f;
        into.Add(new Vector3(origin.X, origin.Y, origin.Z + high));
        into.Add(new Vector3(origin.X, origin.Y, origin.Z + middle));
        into.Add(new Vector3(origin.X, origin.Y, origin.Z + low));
        var reach = HalfWidth + Math.Max(0f, padding);
        foreach (var height in new[] { low, high })
            foreach (var (sx, sy) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) })
                into.Add(new Vector3(origin.X + sx * reach, origin.Y + sy * reach, origin.Z + height));
    }

    /// <summary>True when the straight line from <paramref name="a"/> to <paramref name="b"/> goes through the ball of radius <paramref name="radius"/> round <paramref name="center"/>.</summary>
    public static bool SegmentCrossesSphere(Vector3 a, Vector3 b, Vector3 center, float radius)
    {
        var direction = b - a;
        var lengthSquared = direction.LengthSquared();
        if (lengthSquared < 1e-6f) return Vector3.DistanceSquared(a, center) <= radius * radius;
        var t = Math.Clamp(Vector3.Dot(center - a, direction) / lengthSquared, 0f, 1f);
        return Vector3.DistanceSquared(a + direction * t, center) <= radius * radius;
    }

    /// <summary>What one viewer knows about one enemy.</summary>
    public sealed class PairState
    {
        public int LastVisibleTick;
        public bool Hidden;
    }

    /// <summary>
    /// A seen enemy is shown at once; an unseen one is hidden only after <paramref name="graceTicks"/> ticks without being seen, so a player who
    /// ducks back behind a corner does not flicker. Returns whether the enemy is hidden now.
    /// </summary>
    public static bool Update(PairState state, bool seen, int tick, int graceTicks)
    {
        if (seen)
        {
            state.LastVisibleTick = tick;
            state.Hidden = false;
        }
        else if (tick - state.LastVisibleTick > graceTicks)
        {
            state.Hidden = true;
        }
        return state.Hidden;
    }

    /// <summary>"competitive_5v5,proleague" → the modes this runs on; a server mode counts when it starts with one of them (competitive_5v5_2 matches competitive_5v5).</summary>
    public static bool ModeMatches(string serverMode, string list)
    {
        var mode = (serverMode ?? "").Trim();
        foreach (var wanted in (list ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (mode.StartsWith(wanted, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
