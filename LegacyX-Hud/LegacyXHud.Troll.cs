using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using PanoramaManager;

namespace LegacyXHud;

// Joke cards an admin plays on one player (owner request 2026-11-06, "troll commands that show on the HUD"). LegacyX-Admin sends
//   lx_troll_hud <slot> <kind> <seconds> ["<text>"]       kind: ban | vac | crash | hack | scare | banner
// and this plugin draws the card for that one player (layout legacyx_troll.xml). Every card except the scare turns into "JUST KIDDING" for its last
// third, so nobody is left thinking it was real. Like every card it is not drawn for a player without a team (OpenFor).
public sealed partial class LegacyXHud
{
    private static readonly Regex SafeTrollText = new(@"^[\p{L}\p{N} .,!?'+\-:()]{0,70}$", RegexOptions.Compiled);

    private string trollLayout = LayoutDir + "legacyx_troll.vxml_c";
    private PanelHandle? trollHud;

    private sealed record TrollLook(string Tone, string Title, string Sub, string Foot);

    private static readonly Dictionary<string, TrollLook> TrollLooks = new()
    {
        ["ban"] = new("t-red", "YOU HAVE BEEN BANNED", "Reason: Cheating. Your account will be reviewed.", "LEGACY-X"),
        ["vac"] = new("t-red", "VAC AUTHENTICATION ERROR", "Valve Anti-Cheat was unable to verify your game session.", "STEAM"),
        ["crash"] = new("t-grey", "Counter-Strike 2 has stopped working", "A problem caused the program to stop working correctly. Windows is closing it.", "CS2.EXE"),
        ["hack"] = new("t-amber", "SUSPICIOUS ACTIVITY DETECTED", "Aim assistance patterns were found. Hold still while the server checks you.", "LEGACY-X CHECK"),
    };

    [ConsoleCommand("lx_troll_hud", "A joke card for one player: lx_troll_hud <slot> <ban|vac|crash|hack|scare|banner> <seconds> [\"<text>\"]")]
    public void OnTrollHudCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller != null || !enabled || command.ArgCount < 4) return;
        if (!int.TryParse(command.ArgByIndex(1), out var slot) || !float.TryParse(command.ArgByIndex(3), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds)) return;
        var kind = command.ArgByIndex(2).Trim().ToLowerInvariant();
        var text = command.ArgCount > 4 ? command.ArgByIndex(4).Trim() : "";
        if (!SafeTrollText.IsMatch(text)) return;
        seconds = Math.Clamp(seconds, 2f, 15f);
        if (Utilities.GetPlayerFromSlot(slot) is not { IsValid: true, IsBot: false, IsHLTV: false } player) return;

        var panel = Ensure(ref trollHud, trollLayout, "lx_troll");
        if (panel is null) return;
        if (!panel.IsOpenFor(player)) ForgetMenuState(slot);
        if (!OpenFor(panel, player)) return;   // no team, nothing is drawn

        var steamId = player.SteamID;
        bool Still(out CCSPlayerController now)
        {
            now = Utilities.GetPlayerFromSlot(slot)!;
            return now is { IsValid: true } && now.SteamID == steamId && trollHud is { } h && h.IsOpenFor(now);
        }
        void CloseLater(float after) => AddTimer(after, () => { if (Still(out var p)) trollHud!.Close(p); }, TimerFlags.STOP_ON_MAPCHANGE);

        if (kind == "scare")
        {
            Sv(panel, player, "tr_scare_text", text.Length > 0 ? text.ToUpperInvariant() : "BOO!");
            Sc(panel, player, "troll", "shown", false);
            Sc(panel, player, "scare", "shown", true);
            AddTimer(Math.Min(seconds, 2f), () => { if (Still(out var p)) Sc(trollHud!, p, "scare", "shown", false); }, TimerFlags.STOP_ON_MAPCHANGE);
            CloseLater(Math.Min(seconds, 2f) + 0.4f);
            Console.WriteLine($"[{ModuleName}] The scare is shown to slot {slot}.");
            return;
        }

        TrollLook look;
        if (kind == "banner")
        {
            if (text.Length == 0) return;
            look = new TrollLook("t-green", text, "A message from an admin.", "LEGACY-X");
        }
        else if (!TrollLooks.TryGetValue(kind, out look!)) return;

        Sc(panel, player, "scare", "shown", false);
        Ss(panel, player, "troll", "tone", look.Tone);
        Sv(panel, player, "tr_title", look.Title);
        Sv(panel, player, "tr_sub", look.Sub);
        Sv(panel, player, "tr_foot", look.Foot);
        AddTimer(MenuRiseDelaySeconds, () => { if (Still(out var p)) Sc(trollHud!, p, "troll", "shown", true); }, TimerFlags.STOP_ON_MAPCHANGE);

        if (kind != "banner")
        {
            // The last third: the joke is over.
            AddTimer(seconds * 0.66f, () =>
            {
                if (!Still(out var p)) return;
                Ss(trollHud!, p, "troll", "tone", "t-green");
                Sv(trollHud!, p, "tr_title", "JUST KIDDING");
                Sv(trollHud!, p, "tr_sub", "This was a joke from an admin. Nothing happened to you.");
                Sv(trollHud!, p, "tr_foot", "LEGACY-X");
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }
        AddTimer(seconds, () => { if (Still(out var p)) Sc(trollHud!, p, "troll", "shown", false); }, TimerFlags.STOP_ON_MAPCHANGE);
        CloseLater(seconds + 0.5f);
        Console.WriteLine($"[{ModuleName}] The {kind} card is shown to slot {slot}.");
    }
}
