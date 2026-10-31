using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using PanoramaManager;

namespace LegacyXHud;

// The "HOLD E" hint on the right edge of the screen (owner request 2026-10-31): while the skin menu can be opened (warmup, freeze time) every player
// on a team sees a small card; holding E for half a second opens the menu on their side, like !ws. The card goes when the menu opens or the round starts.
public sealed partial class LegacyXHud
{
    private const float HoldOpenSeconds = 0.5f;

    private string hintLayout = LayoutDir + "legacyx_hint.vxml_c";
    private PanelHandle? hintHud;
    private readonly HashSet<int> hintOn = new();
    private bool hintListening;

    private void HintTick(CCSPlayerController player, Hold hold, bool allowed, bool use)
    {
        var onTeam = player.TeamNum is 2 or 3;
        if (!allowed || !onTeam)
        {
            HintHide(player);
            hold.Open = 0;
            return;
        }
        var panel = Ensure(ref hintHud, hintLayout, "lx_hint");
        if (panel is null) return;
        if (!hintListening)
        {
            // After a world reset the client has lost the card: forget that it was shown, so it is drawn again.
            panel.OnEvent += e => { if (e.Action == PanelAction.Restored && e.Player is { IsValid: true }) hintOn.Remove(e.Player.Slot); };
            hintListening = true;
        }
        if (hintOn.Add(player.Slot))
        {
            if (!OpenFor(panel, player)) { hintOn.Remove(player.Slot); return; }
            panel.SetClassFor(player, "hint", "shown", true);
            hold.OpenStep = -1;
        }
        if (use)
        {
            hold.Open += Server.TickInterval;
            if (hold.Open >= HoldOpenSeconds)
            {
                hold.Open = 0;
                hold.NeedRelease = true;
                HintHide(player);
                OpenSkinMenu(player);
                return;
            }
        }
        else if (hold.Open > 0) hold.Open = 0;

        var step = (int)Math.Round(Math.Clamp(hold.Open / HoldOpenSeconds, 0f, 1f) * 20);
        if (step == hold.OpenStep) return;
        hold.OpenStep = step;
        SetState(panel, player, "hint_fill", "p", "p" + step * 5);
    }

    private void HintHide(CCSPlayerController player)
    {
        if (!hintOn.Remove(player.Slot) || hintHud is null) return;
        if (hintHud.IsOpenFor(player))
        {
            hintHud.SetClassFor(player, "hint", "shown", false);
            hintHud.Close(player);
        }
        foreach (var key in applied.Keys.Where(k => k.Slot == player.Slot && k.PanelId == "hint_fill").ToList()) applied.Remove(key);
    }
}
