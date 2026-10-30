using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using PanoramaManager;

namespace LegacyXHud;

// The knife round side vote card (legacyx_knife): MatchZy (the knife round) tells this plugin who sees what through a server command, and this
// plugin draws it per player. There is no mouse: the winners press 1 or 2, which MatchZy already answers.
//   lx_knife_hud <slot> win|wait <winning team 2|3> <seconds left> <seconds in all>      show / refresh the card for one player
//   lx_knife_hud <slot> off                                                               take it away
public sealed partial class LegacyXHud
{
    [ConsoleCommand("lx_knife_hud", "The knife vote card: lx_knife_hud <slot> win|wait <team> <left> <total> | <slot> off")]
    public void OnKnifeHudCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller != null || !enabled || command.ArgCount < 3) return;
        if (!int.TryParse(command.ArgByIndex(1), out var slot)) return;
        if (Utilities.GetPlayerFromSlot(slot) is not { IsValid: true, IsBot: false } target) return;
        var mode = command.ArgByIndex(2).ToLowerInvariant();
        var panel = Ensure(ref knifeHud, knifeLayout, "lx_knife");
        if (panel is null) return;
        if (mode == "off")
        {
            Sc(panel, target, "kv", "shown", false);
            return;
        }
        if (mode is not ("win" or "wait") || command.ArgCount < 6) return;
        if (!int.TryParse(command.ArgByIndex(3), out var winner) || !int.TryParse(command.ArgByIndex(4), out var left) || !int.TryParse(command.ArgByIndex(5), out var total)) return;
        left = Math.Max(0, left);
        var percent = total > 0 ? Math.Clamp((int)Math.Round(left * 20.0 / total) * 5, 0, 100) : 0;   // the bar has a class for every 5 %
        var winningSide = winner == 2 ? "side-t" : "side-ct";
        Sc(panel, target, "kv_win", "hidden", mode != "win");
        Sc(panel, target, "kv_wait", "hidden", mode != "wait");
        Ss(panel, target, "kv_stay_ic", "side", winningSide);
        Ss(panel, target, "kv_switch_ic", "side", winner == 2 ? "side-ct" : "side-t");
        Ss(panel, target, "kv_wait_ic", "side", winningSide);
        Sv(panel, target, "kv_wait_title", "Enemy team is choosing, please wait!");
        Sv(panel, target, "kv_wait_sub", winner == 2 ? "Terrorists won the knife round: stay or switch" : "Counter-Terrorists won the knife round: stay or switch");
        Sv(panel, target, "kv_time", mode == "win" ? $"{left} s · no answer means stay" : $"{left} s");
        Ss(panel, target, "kv_fill", "p", "p" + percent);
        Sc(panel, target, "kv", "shown", true);
    }
}
