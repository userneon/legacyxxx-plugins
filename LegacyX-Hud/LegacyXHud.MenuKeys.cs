using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;

namespace LegacyXHud;

// Closing the menu without typing: hold R. The server cannot read arbitrary keys, only the game's own buttons (Reload = R).
// A hold has to be continuous; after it fires the key must be let go before it counts again.
public sealed partial class LegacyXHud
{
    private const float HoldCloseSeconds = 0.5f;

    private bool menuKeys = true;
    private readonly HashSet<int> menuOpen = new();

    private sealed class Hold
    {
        public float Close;         // seconds R has been held while the menu is open
        public bool NeedRelease;    // a hold fired: wait for the key to go up
        public int ShownStep = -1;  // last progress step drawn (0..20)
        public float Open;          // seconds E has been held to open the menu (the hint on the right)
        public int OpenStep = -1;   // last step of that bar drawn
    }

    private readonly Dictionary<int, Hold> holds = new();
    private CCSGameRulesProxy? rulesProxy;

    /// <summary>
    /// The menu is for the time before a round: warmup, or the freeze time at the start of a round. Once the round is
    /// live E and R are the game's again, so the menu closes by itself and will not open.
    /// </summary>
    private bool MenuAllowed()
    {
        if (rulesProxy is not { IsValid: true }) rulesProxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
        var rules = rulesProxy?.GameRules;
        return rules is not null && (rules.WarmupPeriod || rules.FreezePeriod);
    }

    private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd e, GameEventInfo info)
    {
        CloseMenuForAll();
        return HookResult.Continue;
    }

    private void CloseMenuForAll()
    {
        if (menu is null) return;
        foreach (var slot in menuOpen.ToList())
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is { IsValid: true }) CloseMenu(menu, player);
            else menuOpen.Remove(slot);
        }
    }

    private void OnMenuKeysTick()
    {
        if (!menuKeys) return;
        var step = Server.TickInterval;
        var allowed = MenuAllowed();
        if (!allowed && menuOpen.Count > 0) CloseMenuForAll();
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

            // !ws opens the menu; holding E (the hint on the right) does too; holding R closes it.
            if (!isOpen) { HintTick(player, hold, allowed, use); continue; }
            HintHide(player);

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
