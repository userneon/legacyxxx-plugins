using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using PanoramaManager;

namespace LegacyXHud;

// The EXP card (owner request 2026-11-07, "a big card with the calculation"): at half time, when overtime starts and when the match is over, MatchZy tells this
// plugin what a player's EXP change was made of, and this plugin draws it for that player as a big card in the middle of the screen (layout legacyx_exp.xml).
// Only numbers travel in the command: a name never goes into a console command. The parts come ten times as large (one decimal, as whole numbers).
//   lx_exp_card <slot> <kind 0 half time|1 overtime|2 match over> <delta> <result*10> <margin*10> <performance*10> <bonus*10> <before> <after> <calibration 1|2> <limited 0|1 daily|2 weekly> <seconds> <rank before 1-18> <rank after 1-18>
public sealed partial class LegacyXHud
{
    private const float ExpSlideSeconds = 0.5f;

    private string expLayout = LayoutDir + "legacyx_exp.vxml_c";
    private PanelHandle? expHud;
    private readonly Dictionary<int, int> expShowIds = new();

    [ConsoleCommand("lx_exp_card", "The EXP card: lx_exp_card <slot> <kind> <delta> <result*10> <margin*10> <performance*10> <bonus*10> <before> <after> <calibration> <limited> <seconds> <rank before> <rank after>")]
    public void OnExpCardCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller != null || !enabled || command.ArgCount < 15) return;
        var n = new int[14];
        for (var i = 1; i <= 14; i++)
            if (!int.TryParse(command.ArgByIndex(i), NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i - 1])) return;
        var (slot, kind, delta, result, margin, performance, bonus, before, after, calibration, limited, seconds, rankBefore, rankAfter) = (n[0], n[1], n[2], n[3], n[4], n[5], n[6], n[7], n[8], n[9], n[10], n[11], n[12], n[13]);
        if (Utilities.GetPlayerFromSlot(slot) is not { IsValid: true, IsBot: false } target) return;
        var panel = Ensure(ref expHud, expLayout, "lx_exp");
        if (panel is null) return;
        // A card that was open is closed first: what the client knows about it is forgotten, or a second card would keep the first one's words.
        if (panel.IsOpenFor(target)) { panel.Close(target); ForgetExpCardState(slot); }
        if (!OpenFor(panel, target)) return;

        var id = expShowIds.TryGetValue(slot, out var previous) ? previous + 1 : 1;
        expShowIds[slot] = id;

        Sv(panel, target, "exp_kind", kind switch { 0 => "HALF TIME", 1 => "OVERTIME", _ => "MATCH OVER" });
        Sv(panel, target, "exp_sub", kind == 2 ? "EXP of this match" : "EXP so far · the final result can still change it");
        Sv(panel, target, "exp_delta", Whole(delta));
        Sv(panel, target, "exp_before", before.ToString("N0", CultureInfo.InvariantCulture));
        Sv(panel, target, "exp_after", after.ToString("N0", CultureInfo.InvariantCulture));
        var parts = new[] { result, margin, performance, bonus };
        for (var i = 0; i < parts.Length; i++)
        {
            Sv(panel, target, $"exp_v{i + 1}", Tenth(parts[i]));
            Ss(panel, target, $"exp_v{i + 1}", "tone", Tone(parts[i]));
        }
        // The rank emblems (the rank-N classes); a rank outside 1 to 18 shows none.
        Ss(panel, target, "exp_rank_b", "rank", rankBefore is >= 1 and <= 18 ? $"rank-{rankBefore}" : null);
        Ss(panel, target, "exp_rank_a", "rank", rankAfter is >= 1 and <= 18 ? $"rank-{rankAfter}" : null);
        Ss(panel, target, "exp_bar", "tone", Tone(delta));
        Ss(panel, target, "exp_delta", "tone", Tone(delta));
        Sc(panel, target, "exp_cal", "hidden", calibration != 2);
        Sv(panel, target, "exp_cal", "×2 calibration");
        var note = limited == 1 ? "Daily limit: gains past 150 EXP a day count for a quarter"
            : limited == 2 ? "Weekly limit reached: no more EXP until Monday"
            : calibration == 2 ? "Calibration: your first ranked matches count double" : "";
        Sc(panel, target, "exp_note", "hidden", note.Length == 0);
        Sv(panel, target, "exp_note", note);
        Ss(panel, target, "exp_note", "tone", limited > 0 ? "limit" : null);

        var steamId = target.SteamID;
        // The flow-in is a transition: the class follows a moment after the card is opened, or it appears at once.
        AddTimer(MenuRiseDelaySeconds, () =>
        {
            if (!ExpStillShown(slot, steamId, id) || expHud is not { } card) return;
            card.SetClassFor(Utilities.GetPlayerFromSlot(slot)!, "exp", "shown", true);
        });
        AddTimer(Math.Clamp(seconds, 3, 30), () =>
        {
            if (!ExpStillShown(slot, steamId, id) || expHud is not { } card) return;
            var player = Utilities.GetPlayerFromSlot(slot)!;
            card.SetClassFor(player, "exp", "shown", false);
            AddTimer(ExpSlideSeconds, () =>
            {
                if (!ExpStillShown(slot, steamId, id) || expHud is not { } open) return;
                open.Close(Utilities.GetPlayerFromSlot(slot)!);
            });
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    /// <summary>The same player is still here, the card is still open for them and no newer card has replaced this one.</summary>
    private bool ExpStillShown(int slot, ulong steamId, int id) =>
        expShowIds.TryGetValue(slot, out var current) && current == id
        && Utilities.GetPlayerFromSlot(slot) is { IsValid: true } player && player.SteamID == steamId
        && expHud is { } card && card.IsOpenFor(player);

    private static string Whole(int value) => value > 0 ? $"+{value}" : value < 0 ? $"−{Math.Abs(value)}" : "0";

    /// <summary>A part with one decimal ("+9.6"); the number came in as tenths.</summary>
    private static string Tenth(int tenths) => tenths == 0 ? "0" : $"{(tenths > 0 ? "+" : "−")}{Math.Abs(tenths) / 10}.{Math.Abs(tenths) % 10}";

    private static string? Tone(int value) => value > 0 ? "up" : value < 0 ? "down" : null;

    private void ForgetExpCardState(int slot)
    {
        if (menuSent.TryGetValue(slot, out var known))
            foreach (var key in known.Keys.Where(key => key.Contains(":exp", StringComparison.Ordinal)).ToList()) known.Remove(key);
        foreach (var key in applied.Keys.Where(key => key.Slot == slot && key.PanelId.StartsWith("exp", StringComparison.Ordinal)).ToList()) applied.Remove(key);
    }
}
