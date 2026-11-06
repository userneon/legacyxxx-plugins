using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using PanoramaManager;

namespace LegacyXHud;

// The welcome card (owner request 2026-11-06): the middle of the screen for a few seconds once a player has chosen a team. LegacyX-Community knows what the site says
// about the player and sends it here as a server command; this plugin draws it for that one player (layout legacyx_welcome.xml). Like every card it is not drawn for
// a player without a team (OpenFor).
//   lx_welcome_hud <slot> <rank id 0-18> <exp> <ladder place 0 = none> <this rank's EXP> <next rank's EXP, 0 = top rank> "<rank name>" "<next rank name>"
public sealed partial class LegacyXHud
{
    private const float WelcomeShownSeconds = 6.5f;
    private const float WelcomeFadeSeconds = 0.7f;
    private static readonly Regex SafeRankName = new(@"^[A-Za-z0-9 ]{0,32}$", RegexOptions.Compiled);

    private string welcomeLayout = LayoutDir + "legacyx_welcome.vxml_c";
    private PanelHandle? welcomeHud;

    [ConsoleCommand("lx_welcome_hud", "The welcome card: lx_welcome_hud <slot> <rank id> <exp> <place> <rank exp> <next exp> \"<rank>\" \"<next rank>\"")]
    public void OnWelcomeHudCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller != null || !enabled || command.ArgCount < 9) return;
        if (!int.TryParse(command.ArgByIndex(1), out var slot)
            || !int.TryParse(command.ArgByIndex(2), out var rankId) || !int.TryParse(command.ArgByIndex(3), out var exp)
            || !int.TryParse(command.ArgByIndex(4), out var place) || !int.TryParse(command.ArgByIndex(5), out var rankExp)
            || !int.TryParse(command.ArgByIndex(6), out var nextExp)) return;
        var rankName = command.ArgByIndex(7).Trim();
        var nextName = command.ArgByIndex(8).Trim();
        if (!SafeRankName.IsMatch(rankName) || !SafeRankName.IsMatch(nextName) || rankId is < 0 or > 18) return;
        if (Utilities.GetPlayerFromSlot(slot) is not { IsValid: true, IsBot: false, IsHLTV: false } player) return;

        var panel = Ensure(ref welcomeHud, welcomeLayout, "lx_welcome");
        if (panel is null) return;
        if (!panel.IsOpenFor(player)) ForgetMenuState(slot);
        if (!OpenFor(panel, player)) return;   // no team, nothing is drawn

        var name = new string(player.PlayerName.Where(c => c >= ' ' && c != '\u007F').ToArray()).Trim();
        Sv(panel, player, "wc_name", name.Length > 0 ? name : "Player");
        var ranked = rankId >= 1 && rankName.Length > 0;
        Sc(panel, player, "wc_rank_row", "hidden", !ranked);
        Sc(panel, player, "wc_bar_box", "hidden", !ranked);
        Sc(panel, player, "wc_norank", "hidden", ranked);
        Sv(panel, player, "wc_norank", "No rank yet. Play a 5v5 match to get one.");
        if (ranked)
        {
            Ss(panel, player, "wc_rank_ic", "rank", "rank-" + rankId);
            Sv(panel, player, "wc_rank_name", rankName);
            Sv(panel, player, "wc_rank_sub", place > 0 ? $"{exp:N0} EXP · #{place} on the ladder" : $"{exp:N0} EXP");
            var percent = 100;
            if (nextExp > rankExp) percent = Math.Clamp((int)Math.Round(Math.Clamp((exp - rankExp) / (double)(nextExp - rankExp), 0, 1) * 20) * 5, 0, 100);   // the bar has a class for every 5 %
            Ss(panel, player, "wc_fill", "p", "p" + percent);
            Sv(panel, player, "wc_bar_left", rankName);
            Sv(panel, player, "wc_bar_right", nextExp > rankExp && nextName.Length > 0 ? $"{Math.Max(0, nextExp - exp):N0} EXP to {nextName}" : "Top rank");
        }
        var steamId = player.SteamID;
        // The fade is a transition: the class follows a moment after the panel is opened, or the card pops up at once (see OpenMenu).
        AddTimer(MenuRiseDelaySeconds, () =>
        {
            if (welcomeHud is not { } card || Utilities.GetPlayerFromSlot(slot) is not { IsValid: true } now || now.SteamID != steamId || !card.IsOpenFor(now)) return;
            Sc(card, now, "welcome", "shown", true);
        });
        Console.WriteLine($"[{ModuleName}] The welcome card is shown to slot {slot}.");

        AddTimer(WelcomeShownSeconds, () =>
        {
            if (Utilities.GetPlayerFromSlot(slot) is not { IsValid: true } again || again.SteamID != steamId || welcomeHud is null || !welcomeHud.IsOpenFor(again)) return;
            Sc(welcomeHud, again, "welcome", "shown", false);
            AddTimer(WelcomeFadeSeconds, () =>
            {
                if (Utilities.GetPlayerFromSlot(slot) is { IsValid: true } last && last.SteamID == steamId && welcomeHud is { } handle && handle.IsOpenFor(last)) handle.Close(last);
            });
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }
}
