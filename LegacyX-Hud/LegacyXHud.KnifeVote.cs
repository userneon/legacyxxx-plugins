using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using PanoramaManager;

namespace LegacyXHud;

// The knife round side vote card (legacyx_knife): MatchZy (the knife round) tells this plugin who sees what through a server command, and this
// plugin draws it per player. The winners click Stay or Switch (a click is the same as typing .stay or .switch, which MatchZy answers).
//   lx_knife_hud <slot> win|wait <winning team 2|3> <seconds left> <seconds in all>      show / refresh the card for one player
//   lx_knife_hud <slot> off                                                               take it away
public sealed partial class LegacyXHud
{
    private readonly HashSet<int> knifeHudLogged = new();
    private PanelHandle? knifeHudWin;
    private bool knifeWinListening;

    private PanelHandle? EnsureKnifeWinCard()
    {
        var handle = Ensure(ref knifeHudWin, knifeLayout, "lx_knife", captureInput: true);
        if (handle is not null && !knifeWinListening)
        {
            handle.OnEvent += OnKnifeCardEvent;
            knifeWinListening = true;
        }
        return handle;
    }

    /// <summary>A click on Stay or Switch is the same as typing .stay or .switch: MatchZy answers it (only the winning team's players count).</summary>
    private void OnKnifeCardEvent(PanelEvent e)
    {
        if (e.Player is not { IsValid: true, IsBot: false } player) return;
        Console.WriteLine($"[{ModuleName}] Knife card event from slot {player.Slot}: {e.Action} {e.ElementId}");
        if (e.Action == PanelAction.Restored)
        {
            ForgetKnifeCardState(player.Slot);
            return;
        }
        if (e.Action != PanelAction.Button) return;
        if (e.ElementId == "kv_btn_stay") player.ExecuteClientCommandFromServer("css_stay");
        else if (e.ElementId == "kv_btn_switch") player.ExecuteClientCommandFromServer("css_switch");
    }

    private void CloseKnifeCards(CCSPlayerController player)
    {
        foreach (var card in new[] { knifeHud, knifeHudWin })
        {
            if (card is null || !card.IsOpenFor(player)) continue;
            Sc(card, player, "kv", "shown", false);
            card.Close(player);
        }
    }

    [ConsoleCommand("lx_knife_hud", "The knife vote card: lx_knife_hud <slot> win|wait <team> <left> <total> | <slot> off")]
    public void OnKnifeHudCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller != null || !enabled || command.ArgCount < 3) return;
        if (!int.TryParse(command.ArgByIndex(1), out var slot)) return;
        if (Utilities.GetPlayerFromSlot(slot) is not { IsValid: true, IsBot: false } target) return;
        var mode = command.ArgByIndex(2).ToLowerInvariant();
        if (mode != "off" && knifeHudLogged.Add(slot)) Console.WriteLine($"[{ModuleName}] Knife vote card for slot {slot}: {mode}.");
        if (mode == "off")
        {
            knifeHudLogged.Remove(slot);
            CloseKnifeCards(target);
            ForgetKnifeCardState(slot);
            return;
        }
        if (mode is not ("win" or "wait") || command.ArgCount < 6) return;
        // The winners get the card that holds the mouse (they click a side); the others get the same card without, so they can still move and look.
        var panel = mode == "win" ? EnsureKnifeWinCard() : Ensure(ref knifeHud, knifeLayout, "lx_knife");
        if (panel is null) return;
        var other = mode == "win" ? knifeHud : knifeHudWin;
        if (other is not null && other.IsOpenFor(target))
        {
            other.Close(target);
            ForgetKnifeCardState(slot);
        }
        if (!int.TryParse(command.ArgByIndex(3), out var winner) || !int.TryParse(command.ArgByIndex(4), out var left) || !int.TryParse(command.ArgByIndex(5), out var total)) return;
        // SetVariableFor / SetClassFor do nothing for a player without a session: the card is opened for them first, and what the client
        // is known to have is forgotten then (a fresh session has nothing).
        if (!panel.IsOpenFor(target))
        {
            ForgetKnifeCardState(slot);
            if (!OpenFor(panel, target)) return;
        }
        left = Math.Max(0, left);
        var percent = total > 0 ? Math.Clamp((int)Math.Round(left * 20.0 / total) * 5, 0, 100) : 0;   // the bar has a class for every 5 %
        var winningSide = winner == 2 ? "side-t" : "side-ct";
        Sc(panel, target, "kv_win", "hidden", mode != "win");
        Sc(panel, target, "kv_wait", "hidden", mode != "wait");
        Ss(panel, target, "kv_stay_ic", "side", winningSide);
        Ss(panel, target, "kv_switch_ic", "side", winner == 2 ? "side-ct" : "side-t");
        // The buttons take the colour of the side they lead to: Stay is the side the winners are on, Switch the other.
        var stayTone = winner == 2 ? "t" : "ct";
        var switchTone = winner == 2 ? "ct" : "t";
        Ss(panel, target, "kv_btn_stay", "opt", "opt-" + stayTone);
        Ss(panel, target, "kv_btn_switch", "opt", "opt-" + switchTone);
        Ss(panel, target, "kv_wait_ic", "side", winningSide);
        Sv(panel, target, "kv_wait_title", "Enemy team is choosing, please wait!");
        Sv(panel, target, "kv_wait_sub", winner == 2 ? "Terrorists won the knife round: stay or switch" : "Counter-Terrorists won the knife round: stay or switch");
        Sv(panel, target, "kv_time", mode == "win" ? $"No answer in {left} s means stay" : $"{left} s");
        Sv(panel, target, "kv_count", left.ToString());
        Sv(panel, target, "kv_stay_hint", winner == 2 ? "Keep Terrorists · attack" : "Keep Counter-Terrorists · defend");
        Sv(panel, target, "kv_switch_hint", winner == 2 ? "Move to Counter-Terrorists · defend" : "Move to Terrorists · attack");
        Ss(panel, target, "kv_fill", "p", "p" + percent);
        Sc(panel, target, "kv", "shown", true);
    }

    private void ForgetKnifeCardState(int slot)
    {
        if (!menuSent.TryGetValue(slot, out var known)) return;
        foreach (var key in known.Keys.Where(key => key.Contains(":kv", StringComparison.Ordinal)).ToList()) known.Remove(key);
        foreach (var key in applied.Keys.Where(key => key.Slot == slot && key.PanelId.StartsWith("kv", StringComparison.Ordinal)).ToList()) applied.Remove(key);
    }
}
