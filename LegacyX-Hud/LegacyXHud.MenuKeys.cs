using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace LegacyXHud;

// The menu without typing: hold E to open it, hold R to close it. The server cannot read arbitrary keys, only the
// game's own buttons (Use = E, Reload = R), so the menu is tied to those. A hold has to be continuous; after it fires
// the key must be let go before it counts again, so one long press never opens and closes the menu in a loop.
public sealed partial class LegacyXHud
{
    private const float HoldOpenSeconds = 0.7f;
    private const float HoldCloseSeconds = 0.5f;

    private bool menuKeys = true;
    private readonly HashSet<int> menuOpen = new();

    private sealed class Hold
    {
        public float Open;          // seconds E has been held while the menu is closed
        public float Close;         // seconds R has been held while the menu is open
        public bool NeedRelease;    // a hold fired: wait for the key to go up
        public int ShownStep = -1;  // last progress step drawn (0..20)
    }

    private readonly Dictionary<int, Hold> holds = new();

    private void OnMenuKeysTick()
    {
        if (!menuKeys) return;
        var step = Server.TickInterval;
        foreach (var player in Utilities.GetPlayers())
        {
            if (player is not { IsValid: true, IsBot: false }) continue;
            if (!holds.TryGetValue(player.Slot, out var hold)) holds[player.Slot] = hold = new Hold();
            var buttons = player.Buttons;
            var use = (buttons & PlayerButtons.Use) != 0;
            var reload = (buttons & PlayerButtons.Reload) != 0;
            var isOpen = menuOpen.Contains(player.Slot);

            if (hold.NeedRelease)
            {
                if (!use && !reload) hold.NeedRelease = false;
                continue;
            }

            if (!isOpen)
            {
                // The knife-round vote already uses E; do not open a menu over it.
                if (!use || knifeVotes.ContainsKey(player.Slot)) { hold.Open = 0; continue; }
                hold.Open += step;
                if (hold.Open < HoldOpenSeconds) continue;
                hold.Open = 0;
                hold.NeedRelease = true;
                OpenMenu(player);
                continue;
            }

            if (!reload)
            {
                if (hold.Close > 0) { hold.Close = 0; DrawHold(player, hold); }
                continue;
            }
            hold.Close += step;
            if (hold.Close >= HoldCloseSeconds)
            {
                hold.Close = 0;
                hold.NeedRelease = true;
                if (menu is not null) CloseMenu(menu, player);
                continue;
            }
            DrawHold(player, hold);
        }
    }

    /// <summary>The bar next to "Hold R to close", in steps of 5 %.</summary>
    private void DrawHold(CCSPlayerController player, Hold hold)
    {
        if (menu is null) return;
        var progress = Math.Clamp(hold.Close / HoldCloseSeconds, 0f, 1f);
        var stepNow = (int)Math.Round(progress * 20);
        if (stepNow == hold.ShownStep) return;
        hold.ShownStep = stepNow;
        SetState(menu, player, "menu_hold_fill", "p", "p" + stepNow * 5);
    }
}
