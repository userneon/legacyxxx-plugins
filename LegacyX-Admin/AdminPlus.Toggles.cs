using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminPlus;

/// <summary>
/// One command instead of two (owner request 2026-11-06): css_blind blinds a player and, when the player is already blind, lifts it;
/// the same for freeze, bury, shake, drug, speed and size. The old css_unblind (and the other css_un… ones) still work for scripts and
/// share the permission of the command they undo, but the menus, the Roles list on the website and the help no longer list them.
/// A target counts as "on" when the plugin knows it applied the effect; with several targets the effect is lifted only when all of them
/// have it, otherwise it is applied to everyone.
/// </summary>
public partial class AdminPlus
{
    private readonly HashSet<ulong> _toggledBury = new();
    private readonly HashSet<ulong> _toggledSpeed = new();
    private readonly HashSet<ulong> _toggledSize = new();

    /// <summary>The commands that used to have an "un" twin: the twin is checked as the command it undoes.</summary>
    private static readonly Dictionary<string, string> ToggleTwins = new(StringComparer.OrdinalIgnoreCase)
    {
        ["unfreeze"] = "freeze", ["unbury"] = "bury", ["unshake"] = "shake", ["unblind"] = "blind", ["undrug"] = "drug", ["unspeed"] = "speed", ["unsize"] = "size",
    };

    private void ClearToggledFun(ulong steamId)
    {
        _toggledBury.Remove(steamId);
        _toggledSpeed.Remove(steamId);
        _toggledSize.Remove(steamId);
    }

    private bool HasFunTimer(CCSPlayerController p, FunTimer key) => _funTimers.TryGetValue(p.SteamID, out var map) && map.ContainsKey(key);
    private bool IsFrozen(CCSPlayerController p) => _freezeAnchor.ContainsKey(p.SteamID) || HasFunTimer(p, FunTimer.Freeze) || HasFunTimer(p, FunTimer.FreezeLoop);
    private bool IsShaking(CCSPlayerController p) => _activeShakes.ContainsKey(p.SteamID) || HasFunTimer(p, FunTimer.Shake);

    private void ToggleFun(CCSPlayerController? caller, CommandInfo info, bool onlyAlive, Func<CCSPlayerController, bool> active,
        Action<CCSPlayerController?, CommandInfo> on, Action<CCSPlayerController?, CommandInfo> off, HashSet<ulong>? tracked = null, int minArgs = 2)
    {
        List<CCSPlayerController> players = new();
        var resolved = info.ArgCount >= 2 && ResolveTargets(caller, info.GetArg(1), true, onlyAlive, false, out players, out _, out _, out _, true);
        if (resolved && players.Count > 0 && players.All(active))
        {
            off(caller, info);
            if (tracked != null) foreach (var p in players) tracked.Remove(p.SteamID);
            return;
        }
        on(caller, info);
        // Only remember an effect the command really applied (a missing value prints the usage and does nothing).
        if (tracked != null && resolved && info.ArgCount >= minArgs) foreach (var p in players) tracked.Add(p.SteamID);
    }
}
