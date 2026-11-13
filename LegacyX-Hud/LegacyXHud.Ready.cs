using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using PanoramaManager;

namespace LegacyXHud;

// The "Press R to ready" card (legacyx_ready, owner request 2026-11-13): MatchZy (which reads the R key and keeps who is ready) tells this plugin what each player
// sees through a server command, and this plugin draws it per player in place of MatchZy's centre HTML text.
//   lx_ready_hud <slot> yes|no <ready> <needed>     show / refresh the card for one player (yes: this player is ready)
//   lx_ready_hud <slot> off                         take it away
public sealed partial class LegacyXHud
{
    private string readyLayout = LayoutDir + "legacyx_ready.vxml_c";
    private PanelHandle? readyHud;
    private readonly HashSet<int> readyShown = new();

    [ConsoleCommand("lx_ready_hud", "The ready card: lx_ready_hud <slot> yes|no <ready> <needed> | <slot> off")]
    public void OnReadyHudCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller != null || !enabled || command.ArgCount < 3) return;
        if (!int.TryParse(command.ArgByIndex(1), out var slot)) return;
        if (Utilities.GetPlayerFromSlot(slot) is not { IsValid: true, IsBot: false } target) return;
        var mode = command.ArgByIndex(2).ToLowerInvariant();
        if (mode == "off")
        {
            if (readyShown.Remove(slot) && readyHud is { } open && open.IsOpenFor(target))
            {
                Sc(open, target, "ready", "shown", false);
                open.Close(target);
            }
            ForgetReadyCardState(slot);
            return;
        }
        if (mode is not ("yes" or "no") || command.ArgCount < 5) return;
        if (!int.TryParse(command.ArgByIndex(3), out var count) || !int.TryParse(command.ArgByIndex(4), out var needed)) return;
        var panel = Ensure(ref readyHud, readyLayout, "lx_ready");
        if (panel is null) return;
        // A card closed on the client has forgotten what was sent for it: open it first, and forget what was known.
        if (!panel.IsOpenFor(target))
        {
            ForgetReadyCardState(slot);
            if (!OpenFor(panel, target)) return;
        }
        var mine = mode == "yes";
        Sv(panel, target, "ready_title", mine ? "You are ready" : "Press R to ready");
        Sv(panel, target, "ready_sub", mine ? $"R to unready · {count}/{needed} ready" : $"{count}/{needed} ready");
        Ss(panel, target, "ready", "kind", mine ? "ready-yes" : "ready-no");
        Sc(panel, target, "ready", "shown", true);
        if (readyShown.Add(slot)) Console.WriteLine($"[{ModuleName}] The ready card is shown to slot {slot}.");
    }

    private void ForgetReadyCardState(int slot)
    {
        if (!menuSent.TryGetValue(slot, out var known)) return;
        foreach (var key in known.Keys.Where(key => key.Contains(":ready", StringComparison.Ordinal)).ToList()) known.Remove(key);
        foreach (var key in applied.Keys.Where(key => key.Slot == slot && key.PanelId.StartsWith("ready", StringComparison.Ordinal)).ToList()) applied.Remove(key);
    }
}
