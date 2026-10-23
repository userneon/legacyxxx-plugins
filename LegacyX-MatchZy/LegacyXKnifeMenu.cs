using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;

namespace MatchZy;

/// <summary>
/// The knife round side vote as the numbered menu on the left of the screen (owner request 2026-10-23): the winners see
/// "1. Stay  2. Switch" there and press the 1 or 2 key. The classic menu message (ShowMenu) is sent, and the answer comes back as the
/// client command menuselect; the same text is also drawn in the middle of the screen, and the 1 / 2 weapon slot keys, .stay and
/// .switch in chat all work too, so a client that does not draw the left menu can still vote. No answer in
/// <see cref="KnifeVoteSeconds"/> means stay.
/// </summary>
public partial class MatchZy
{
    private CounterStrikeSharp.API.Modules.Timers.Timer? knifeMenuTimer;
    private float knifeMenuEndsAt;
    private bool knifeMenuOnScreen;

    // Which message draws the left menu: "cs" = CCSUsrMsg_ShowMenu (354), "base" = CUserMessageShowMenu (134), "off" = only the centre text.
    // Whether a CS2 client draws either one cannot be told from the server, so it can be switched live: lx_knife_menu_msg cs|base|off.
    private string knifeMenuMessage = "cs";

    private void RegisterLegacyXKnifeMenu()
    {
        AddCommandListener("menuselect", (player, info) =>
            info.ArgCount > 1 && int.TryParse(info.ArgByIndex(1), out var choice) && choice is 1 or 2 ? OnKnifeMenuKey(player, stay: choice == 1) : HookResult.Continue);
        AddCommandListener("slot1", (player, info) => OnKnifeMenuKey(player, stay: true));
        AddCommandListener("slot2", (player, info) => OnKnifeMenuKey(player, stay: false));
    }

    [ConsoleCommand("lx_knife_menu_msg", "Knife vote menu on the left of the screen: lx_knife_menu_msg cs|base|off")]
    public void OnKnifeMenuMessage(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null) return; // server console / RCON only
        var wanted = command.ArgCount > 1 ? command.ArgByIndex(1).Trim().ToLowerInvariant() : "";
        if (wanted is "cs" or "base" or "off") knifeMenuMessage = wanted;
        command.ReplyToCommand($"[MatchZy] lx_knife_menu_msg is {knifeMenuMessage}. Use cs, base or off.");
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
        foreach (var player in Utilities.GetPlayers())
            if (IsPlayerValid(player) && !player.IsBot && !player.IsHLTV && player.TeamNum == knifeWinner)
                SendKnifeMenu(player, open: true);
        knifeMenuOnScreen = true;
        DrawLegacyXKnifeMenu();
    }

    private void StopLegacyXKnifeMenu()
    {
        knifeMenuTimer?.Kill();
        knifeMenuTimer = null;
        if (!knifeMenuOnScreen) return;
        knifeMenuOnScreen = false;
        foreach (var player in Utilities.GetPlayers())
            if (IsPlayerValid(player) && !player.IsBot && !player.IsHLTV)
                SendKnifeMenu(player, open: false);
    }

    /// <summary>Opens (or closes, with no slots and no time) the numbered menu on one player's screen.</summary>
    private void SendKnifeMenu(CCSPlayerController player, bool open)
    {
        if (knifeMenuMessage == "off") return;
        try
        {
            var text = open ? "You won the knife round\n->1. Stay on your side\n->2. Switch sides\n" : "";
            var slots = open ? 0b11 : 0;
            var seconds = open ? (int)KnifeVoteSeconds : 0;
            UserMessage message;
            if (knifeMenuMessage == "base")
            {
                message = UserMessage.FromId(134);
                message.SetInt("validslots", slots);
                message.SetInt("displaytime", seconds);
                message.SetString("menustring", text);
            }
            else
            {
                message = UserMessage.FromId(354);
                message.SetInt("bits_valid_slots", slots);
                message.SetInt("display_time", seconds);
                message.SetString("menu_string", text);
            }
            message.Recipients.Add(player);
            message.Send();
        }
        catch (Exception exception)
        {
            Log($"[KnifeMenu] The menu message could not be sent: {exception.Message}");
        }
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
