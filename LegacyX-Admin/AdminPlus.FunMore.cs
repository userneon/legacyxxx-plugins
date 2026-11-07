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
/// small (the model, and since 2026-11-07 the hit box follows it: owner request "make the hit box follow and grow"), css_unsize puts it back. Same targets and permission as the other fun
/// commands (@css/slay); the !admin menu has entries for them under Player FX.
/// </summary>
public partial class AdminPlus
{
    private const float LaunchMin = 100f, LaunchMax = 1500f, LaunchDefault = 600f;
    private const float SizeMin = 0.3f, SizeMax = 3f;

    private void RegisterFunMoreCommands()
    {
        AddPermCmd("@css/slay", "css_launch", "<#userid|name|all @ commands> [power]", CmdLaunch);
        AddPermCmd("@css/slay", "css_size", "<#userid|name|all @ commands> <0.3-3>", (c, i) => ToggleFun(c, i, true, p => _toggledSize.Contains(p.SteamID), CmdSize, CmdUnSize, _toggledSize, 3));
        AddPermCmd("@css/slay", "css_unsize", "<#userid|name|all @ commands>", CmdUnSize);
        RegisterFunCheatCommands();
        RegisterFunExtraCommands();
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

    /// <summary>A standing player's collision box (units); the hit box of a size 1 player.</summary>
    private const float BodyHalfWidth = 16f, BodyHeight = 72f;

    /// <summary>
    /// The model's size and its hit box (the game puts both back by itself on the next spawn). The model is scaled through its scene node and the
    /// SetScale input; the box a bullet is first tested against (the pawn's collision bounds, and the bounds around the whole model) is scaled
    /// with it, so a big player can be hit where they look big and a small one is not hit in empty air.
    /// </summary>
    private static void SetSize(CCSPlayerController target, float scale)
    {
        var pawn = target.PlayerPawn?.Value;
        var node = pawn?.CBodyComponent?.SceneNode;
        if (pawn == null || !pawn.IsValid || node == null) return;
        node.Scale = scale;
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_CBodyComponent");
        pawn.AcceptInput("SetScale", pawn, pawn, scale.ToString("0.###", CultureInfo.InvariantCulture));
        var box = pawn.Collision;
        if (box == null) return;
        var half = BodyHalfWidth * scale;
        var height = BodyHeight * scale;
        box.Mins.X = -half; box.Mins.Y = -half; box.Mins.Z = 0f;
        box.Maxs.X = half; box.Maxs.Y = half; box.Maxs.Z = height;
        box.SurroundingMins.X = -half; box.SurroundingMins.Y = -half; box.SurroundingMins.Z = 0f;
        box.SurroundingMaxs.X = half; box.SurroundingMaxs.Y = half; box.SurroundingMaxs.Z = height;
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_Collision");
    }
}
