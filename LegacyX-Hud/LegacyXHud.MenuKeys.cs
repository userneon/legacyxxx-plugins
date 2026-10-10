using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
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
        public int HintMode;        // 0 = hint hidden, 1 = "Hold E to open menu", 2 = "Hold R to close menu"
        public int HintStep = -1;   // last hint fill step drawn (0..20)
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
            var adminOpen = adminViews.ContainsKey(player.Slot);
            var isOpen = menuOpen.Contains(player.Slot) || adminOpen;

            UpdateHint(player, hold, isOpen ? 2 : allowed && !knifeVotes.ContainsKey(player.Slot) ? 1 : 0);

            if (hold.NeedRelease)
            {
                if (!use && !reload) hold.NeedRelease = false;
                continue;
            }

            if (!isOpen)
            {
                // The knife-round vote already uses E; do not open a menu over it.
                if (!allowed || !use || knifeVotes.ContainsKey(player.Slot)) { hold.Open = 0; continue; }
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
                if (adminOpen) CloseAdmin(player);
                else if (menu is not null) CloseMenu(menu, player);
                continue;
            }
            DrawHold(player, hold);
        }
    }

    /// <summary>The bar next to "Hold R to close", in steps of 5 %.</summary>
    private void DrawHold(CCSPlayerController player, Hold hold)
    {
        if (adminViews.ContainsKey(player.Slot))
        {
            if (admin is null) return;
            var adminStep = (int)Math.Round(Math.Clamp(hold.Close / HoldCloseSeconds, 0f, 1f) * 20);
            if (adminStep == hold.ShownStep) return;
            hold.ShownStep = adminStep;
            As(player, "adm_hold_fill", "p", "p" + adminStep * 5);
            return;
        }
        if (menu is null) return;
        var progress = Math.Clamp(hold.Close / HoldCloseSeconds, 0f, 1f);
        var stepNow = (int)Math.Round(progress * 20);
        if (stepNow == hold.ShownStep) return;
        hold.ShownStep = stepNow;
        SetState(menu, player, "menu_hold_fill", "p", "p" + stepNow * 5);
    }

    /// <summary>
    /// The hint at the top right: "Hold E to open menu" while the menu can be opened (mode 1), "Hold R to close menu" while
    /// it is open (mode 2), each with a bar that fills as the key is held. Only writes to the client when the mode or the
    /// 5 % step changes.
    /// </summary>
    private void UpdateHint(CCSPlayerController player, Hold hold, int mode)
    {
        var panel = notify;
        if (panel is null) return;
        if (mode == 0)
        {
            if (hold.HintMode == 0) return;
            hold.HintMode = 0;
            if (panel.IsOpenFor(player)) panel.SetClassFor(player, "hint", "open", false);
            return;
        }
        if (hold.HintMode != mode)
        {
            if (!OpenFor(panel, player)) return;
            var wasHidden = hold.HintMode == 0;
            hold.HintMode = mode;
            hold.HintStep = -1;
            panel.SetVariableFor(player, "hint_key", mode == 1 ? "E" : "R");
            panel.SetVariableFor(player, "hint_text", mode == 1 ? "Hold E to open menu" : "Hold R to close menu");
            if (wasHidden) panel.SetClassFor(player, "hint", "open", true);
        }
        var progress = mode == 1 ? hold.Open / HoldOpenSeconds : hold.Close / HoldCloseSeconds;
        var stepNow = (int)Math.Round(Math.Clamp(progress, 0f, 1f) * 20);
        if (stepNow == hold.HintStep) return;
        hold.HintStep = stepNow;
        SetState(panel, player, "hint_fill", "p", "p" + stepNow * 5);
    }
}
