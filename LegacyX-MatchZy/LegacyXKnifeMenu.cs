using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;

namespace MatchZy;

/// <summary>
/// The knife round side vote as a numbered menu (owner request 2026-10-23): the winners see "[1] Stay  [2] Switch" in the middle of the
/// screen and press the 1 or 2 key (the weapon slot keys, held back while the menu is open); the other players see who is choosing.
/// .stay and .switch in chat still work, and no answer in <see cref="KnifeVoteSeconds"/> means stay.
/// </summary>
public partial class MatchZy
{
    private CounterStrikeSharp.API.Modules.Timers.Timer? knifeMenuTimer;
    private float knifeMenuEndsAt;

    private void RegisterLegacyXKnifeMenu()
    {
        AddCommandListener("slot1", (player, info) => OnKnifeMenuKey(player, stay: true));
        AddCommandListener("slot2", (player, info) => OnKnifeMenuKey(player, stay: false));
    }

    private HookResult OnKnifeMenuKey(CCSPlayerController? player, bool stay)
    {
        if (!isSideSelectionPhase || knifeMenuTimer == null || player == null || !player.IsValid || player.IsBot || player.TeamNum != knifeWinner) return HookResult.Continue;
        if (stay) OnTeamStay(player, null);
        else OnTeamSwitch(player, null);
        return HookResult.Stop;
    }

    private void StartLegacyXKnifeMenu()
    {
        knifeMenuEndsAt = Server.CurrentTime + KnifeVoteSeconds;
        knifeMenuTimer?.Kill();
        knifeMenuTimer = AddTimer(0.5f, DrawLegacyXKnifeMenu, TimerFlags.REPEAT);
        DrawLegacyXKnifeMenu();
    }

    private void StopLegacyXKnifeMenu()
    {
        knifeMenuTimer?.Kill();
        knifeMenuTimer = null;
    }

    private void DrawLegacyXKnifeMenu()
    {
        if (!isSideSelectionPhase)
        {
            StopLegacyXKnifeMenu();
            return;
        }

        var left = Math.Max(0, (int)Math.Ceiling(knifeMenuEndsAt - Server.CurrentTime));
        var winners = $"<font class='fontSize-m' color='#fafafa'>You won the knife round</font><br>"
            + "<font class='fontSize-m' color='#fafafa'>[<font color='#a78bfa'>1</font>] Stay &nbsp;&nbsp; [<font color='#a78bfa'>2</font>] Switch</font><br>"
            + $"<font class='fontSize-s' color='#a3a3a3'>{left}s · no answer means stay</font>";
        var others = $"<font class='fontSize-m' color='#fafafa'>{System.Net.WebUtility.HtmlEncode(knifeWinnerName)} won the knife round</font><br>"
            + $"<font class='fontSize-s' color='#a3a3a3'>choosing a side · {left}s</font>";

        foreach (var player in Utilities.GetPlayers())
        {
            if (!IsPlayerValid(player) || player.IsBot || player.IsHLTV) continue;
            player.PrintToCenterHtml(player.TeamNum == knifeWinner ? winners : others);
        }
    }
}
