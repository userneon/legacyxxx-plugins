using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Globalization;
using System.Linq;

namespace AdminPlus;

/// <summary>
/// More for the Fun menu (owner request 2026-10-18): css_launch throws a player up in the air, css_size makes a player big or
/// small (the model only: the hit box stays the same), css_unsize puts it back. Same targets and permission as the other fun
/// commands (@css/slay); the !admin menu has entries for them under Player FX.
/// </summary>
public partial class AdminPlus
{
    private const float LaunchMin = 100f, LaunchMax = 1500f, LaunchDefault = 600f;
    private const float SizeMin = 0.3f, SizeMax = 3f;

    private void RegisterFunMoreCommands()
    {
        AddPermCmd("@css/slay", "css_launch", "<#userid|name|all @ commands> [power]", CmdLaunch);
        AddPermCmd("@css/slay", "css_size", "<#userid|name|all @ commands> <0.3-3>", CmdSize);
        AddPermCmd("@css/slay", "css_unsize", "<#userid|name|all @ commands>", CmdUnSize);
        RegisterFunCheatCommands();
    }

    private void CmdLaunch(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Launch.Usage", "Usage: css_launch <target> [power]"); return; }
        var power = LaunchDefault;
        if (info.ArgCount >= 3 && float.TryParse(info.GetArg(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var asked)) power = asked;
        power = Math.Clamp(power, LaunchMin, LaunchMax);
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }
        foreach (var target in players)
        {
            var pawn = target.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid) continue;
            var velocity = pawn.AbsVelocity;
            pawn.Teleport(null, null, new Vector(velocity.X, velocity.Y, power));
        }
        Announce("css_launch<player>", "css_launch<multiple>", admin, label, power.ToString("0"));
        AddTimer(0.1f, () =>
        {
            foreach (var target in players)
                _ = Discord.SendAdminActionLog("launch", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, power.ToString("0"), this);
        });
    }

    private void CmdSize(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 3 || !float.TryParse(info.GetArg(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
        { SendUsageMessage(caller, "Fun.Size.Usage", "Usage: css_size <target> <0.3-3>"); return; }
        scale = Math.Clamp(scale, SizeMin, SizeMax);
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }
        foreach (var target in players) SetSize(target, scale);
        Announce("css_size<player>", "css_size<multiple>", admin, label, scale.ToString("0.##"));
        AddTimer(0.1f, () =>
        {
            foreach (var target in players)
                _ = Discord.SendAdminActionLog("size", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, scale.ToString("0.##"), this);
        });
    }

    private void CmdUnSize(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Size.Usage", "Usage: css_unsize <target>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }
        foreach (var target in players) SetSize(target, 1.0f);
        Announce("css_size<player>", "css_size<multiple>", admin, label, "1");
        AddTimer(0.1f, () =>
        {
            foreach (var target in players)
                _ = Discord.SendAdminActionLog("unsize", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, "", this);
        });
    }

    /// <summary>The model's size; the game puts it back by itself on the next spawn.</summary>
    private static void SetSize(CCSPlayerController target, float scale)
    {
        var pawn = target.PlayerPawn?.Value;
        var node = pawn?.CBodyComponent?.SceneNode;
        if (pawn == null || node == null) return;
        node.Scale = scale;
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_CBodyComponent");
    }
}
