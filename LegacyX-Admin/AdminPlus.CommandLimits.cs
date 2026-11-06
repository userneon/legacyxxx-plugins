using CounterStrikeSharp.API.Core;
using System;
using System.Collections.Generic;

namespace AdminPlus;

/// <summary>
/// Limits on a command for a role, set on the staff site (Staff → Roles → a command's Limit): a wait between two uses and a cap per map.
/// They come with the role (CustomGrant.Limits), so a change there reaches the server within a minute. Counted per player and per command
/// from the chat, the console and the menus alike; a use is counted when it is allowed to run. The counts start over with each map.
/// </summary>
public partial class AdminPlus
{
    private readonly Dictionary<(ulong, string), DateTime> _limitLastUse = new();
    private readonly Dictionary<(ulong, string), int> _limitUses = new();

    private void ResetCommandLimitCounts()
    {
        _limitLastUse.Clear();
        _limitUses.Clear();
    }

    /// <summary>True when the command may run now; false (with a short note to the player) when its cooldown or its cap on this map says no.</summary>
    private bool TryUseLimitedCommand(CCSPlayerController caller, string command)
    {
        if (!adminCommandLimits.TryGetValue(caller.SteamID, out var limits) || !limits.TryGetValue(command, out var limit)) return true;
        var key = (caller.SteamID, command);
        var now = DateTime.UtcNow;
        _limitUses.TryGetValue(key, out var used);
        if (limit.PerMap > 0 && used >= limit.PerMap)
        {
            caller.PrintToChat(LegacyXChat.System($"!{command} is limited to {limit.PerMap} use{(limit.PerMap == 1 ? "" : "s")} per map. You used it {used}."));
            return false;
        }
        if (limit.CooldownSeconds > 0 && _limitLastUse.TryGetValue(key, out var last))
        {
            var wait = (int)Math.Ceiling(limit.CooldownSeconds - (now - last).TotalSeconds);
            if (wait > 0)
            {
                caller.PrintToChat(LegacyXChat.System($"Wait {wait} s before using !{command} again."));
                return false;
            }
        }
        _limitLastUse[key] = now;
        _limitUses[key] = used + 1;
        return true;
    }
}
