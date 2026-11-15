using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using PanoramaManager;

namespace LegacyXHud;

// The scoreboard strip (legacyx_board, owner request 2026-11-15): while a player holds Tab, a small card at the top centre of the game's scoreboard says whose server this is
// and where the site is. The game's own scoreboard text cannot be changed by an addon, so this plugin reads the Tab key and shows a card of its own to that player.
//   LEGACYX_HUD_BOARD=false           switches it off
//   LEGACYX_HUD_BOARD_SITE            the address shown (default legacyx.cc)
//   LEGACYX_HUD_BOARD_LAYOUT          the layout path
public sealed partial class LegacyXHud
{
    private string boardLayout = LayoutDir + "legacyx_board.vxml_c";
    private string boardSite = "legacyx.cc";
    private PanelHandle? boardHud;
    private readonly HashSet<int> boardHeld = new();
    private int boardTick;

    private void RegisterBoard(LegacyX.Shared.Configuration.LegacyXEnvironment env)
    {
        if (!env.GetModuleBoolean("HUD", "BOARD", true)) return;
        boardLayout = env.Get("LEGACYX_HUD_BOARD_LAYOUT", boardLayout);
        var site = env.Get("LEGACYX_HUD_BOARD_SITE", boardSite).Trim();
        if (site.Length is > 0 and <= 40 && site.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '/' or ':')) boardSite = site;
        RegisterListener<Listeners.OnTick>(OnBoardTick);
    }

    private static string ServerTitle()
    {
        var name = ConVar.Find("hostname")?.StringValue ?? "";
        name = new string(name.Where(c => c >= ' ' && c != '\u007F').ToArray()).Trim();
        if (name.Length > 48) name = name[..48].TrimEnd();
        return name.Length > 0 ? name : "LXC";
    }

    private void OnBoardTick()
    {
        // Four checks a second are plenty for a card that fades in: the tick runs 64 times.
        if (++boardTick % 8 != 0) return;
        foreach (var player in Utilities.GetPlayers())
        {
            if (player is not { IsValid: true, IsBot: false, IsHLTV: false }) continue;
            var slot = player.Slot;
            var held = (player.Buttons & PlayerButtons.Scoreboard) != 0;
            var was = boardHeld.Contains(slot);
            if (held == was) continue;
            if (held)
            {
                var panel = Ensure(ref boardHud, boardLayout, "lx_board");
                if (panel is null) return;
                if (!panel.IsOpenFor(player)) ForgetBoardState(slot);
                if (!OpenFor(panel, player)) continue;   // no team, nothing is drawn
                boardHeld.Add(slot);
                Sv(panel, player, "board_server", ServerTitle());
                Sv(panel, player, "board_site", boardSite);
                Sc(panel, player, "board", "shown", true);
            }
            else
            {
                boardHeld.Remove(slot);
                if (boardHud is { } open && open.IsOpenFor(player)) Sc(open, player, "board", "shown", false);
            }
        }
    }

    private void ForgetBoardState(int slot)
    {
        if (!menuSent.TryGetValue(slot, out var known)) return;
        foreach (var key in known.Keys.Where(key => key.Contains(":board", StringComparison.Ordinal)).ToList()) known.Remove(key);
        foreach (var key in applied.Keys.Where(key => key.Slot == slot && key.PanelId.StartsWith("board", StringComparison.Ordinal)).ToList()) applied.Remove(key);
    }
}
