using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using PanoramaManager;
using CssTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace LegacyXHud;

// The knife-round side vote for the team that won. MatchZy starts and ends the phase (lx_hud_knife start|stop)
// and applies the result (lx_knife_choice); this only draws the vote and counts it.
//
// Keyboard, no mouse: A / D moves the highlight (starts on Stay), E confirms it. The choice is not locked by E:
// A / D afterwards moves the highlight and takes the confirmation back until E is pressed again. Only confirmed
// votes count; most votes wins, a tie or no votes is Stay. Votes are secret (nothing shows a count or a name).
public sealed partial class LegacyXHud
{
    private const int KnifeSeconds = 5;

    private sealed class Vote
    {
        public int Selected;      // 0 = Stay, 1 = Switch
        public bool Confirmed;
        public PlayerButtons Previous;
    }

    private readonly Dictionary<int, Vote> knifeVotes = new();
    private int knifeTeam;
    private int knifeLeft;
    private CssTimer? knifeTimer;

    /// <summary>Server console (MatchZy): lx_hud_knife start &lt;2|3&gt; | stop</summary>
    [ConsoleCommand("lx_hud_knife", "Knife round side vote: lx_hud_knife start <winning team 2|3> | stop")]
    public void OnKnifeCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!enabled || caller != null) return;
        switch (command.GetArg(1).ToLowerInvariant())
        {
            case "start" when int.TryParse(command.GetArg(2), out var team) && team is 2 or 3:
                KnifeStart(team);
                break;
            case "stop":
                KnifeStop();
                break;
        }
    }

    private static string TeamName(int team) => team == 2 ? "Terrorists" : "Counter-Terrorists";

    private void KnifeStart(int team)
    {
        KnifeStop();
        var panel = EnsureKnife();
        if (panel is null) return;

        var winners = Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false } && p.TeamNum == team).ToList();
        var other = team == 2 ? 3 : 2;
        foreach (var loser in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false } && p.TeamNum == other))
            Announce(loser, "Knife round lost", $"{TeamName(team)} are choosing the side.");
        if (winners.Count == 0) return;

        knifeTeam = team;
        knifeLeft = KnifeSeconds;
        var stayT = team == 2;
        foreach (var player in winners)
        {
            if (!OpenFor(panel, player)) continue;
            knifeVotes[player.Slot] = new Vote { Previous = player.Buttons };
            panel.SetVariableFor(player, "knife_count", KnifeSeconds.ToString());
            panel.SetVariableFor(player, "knife_sub", $"Stay on {TeamName(team)} or switch to {TeamName(other)}. Most votes wins.");
            panel.SetVariableFor(player, "knife_stay_sub", $"Keep {TeamName(team)} · {(stayT ? "attack" : "defend")}");
            panel.SetVariableFor(player, "knife_switch_sub", $"Move to {TeamName(other)} · {(stayT ? "defend" : "attack")}");
            SetState(panel, player, "knife_stay_side", "side", stayT ? "side-t" : "side-ct");
            SetState(panel, player, "knife_switch_side", "side", stayT ? "side-ct" : "side-t");
            SetState(panel, player, "knife_fill", "p", "p100");
            ShowVote(panel, player, knifeVotes[player.Slot]);
            panel.SetClassFor(player, "knife", "shown", true);
        }
        knifeTimer = AddTimer(1f, KnifeSecond, TimerFlags.REPEAT);
        Console.WriteLine($"[{ModuleName}] Knife vote for {TeamName(team)}: {knifeVotes.Count} voter(s)");
    }

    /// <summary>Highlight (sel) and confirmation (mine) of the two cards for one player.</summary>
    private void ShowVote(PanelHandle panel, CCSPlayerController player, Vote vote)
    {
        panel.SetClassFor(player, "knife_stay", "sel", vote.Selected == 0);
        panel.SetClassFor(player, "knife_switch", "sel", vote.Selected == 1);
        panel.SetClassFor(player, "knife_stay", "mine", vote.Confirmed && vote.Selected == 0);
        panel.SetClassFor(player, "knife_switch", "mine", vote.Confirmed && vote.Selected == 1);
    }

    private void OnKnifeTick()
    {
        if (knifeVotes.Count == 0 || knife is null) return;
        foreach (var (slot, vote) in knifeVotes)
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is not { IsValid: true }) continue;
            var now = player.Buttons;
            var pressed = now & ~vote.Previous;   // only the moment a key goes down
            vote.Previous = now;
            if (pressed == 0) continue;

            var changed = false;
            if ((pressed & PlayerButtons.Moveleft) != 0 && vote.Selected != 0) { vote.Selected = 0; vote.Confirmed = false; changed = true; }
            if ((pressed & PlayerButtons.Moveright) != 0 && vote.Selected != 1) { vote.Selected = 1; vote.Confirmed = false; changed = true; }
            if ((pressed & PlayerButtons.Use) != 0 && !vote.Confirmed) { vote.Confirmed = true; changed = true; }
            if (changed) ShowVote(knife, player, vote);
        }
    }

    private void KnifeSecond()
    {
        if (knife is null) { KnifeStop(); return; }
        knifeLeft--;
        foreach (var slot in knifeVotes.Keys)
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is not { IsValid: true }) continue;
            knife.SetVariableFor(player, "knife_count", Math.Max(knifeLeft, 0).ToString());
            SetState(knife, player, "knife_fill", "p", "p" + Math.Max(knifeLeft, 0) * 100 / KnifeSeconds);
        }
        if (knifeLeft > 0) return;

        var stay = knifeVotes.Values.Count(v => v.Confirmed && v.Selected == 0);
        var swap = knifeVotes.Values.Count(v => v.Confirmed && v.Selected == 1);
        var choice = swap > stay ? "switch" : "stay";
        var voter = knifeVotes.Keys.FirstOrDefault(-1);
        var team = knifeTeam;
        Console.WriteLine($"[{ModuleName}] Knife vote result: {choice} (stay {stay}, switch {swap})");
        KnifeStop();
        if (voter >= 0) Server.ExecuteCommand($"lx_knife_choice {voter} {choice}");
        var sideNow = choice == "stay" ? team : (team == 2 ? 3 : 2);
        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false } && p.TeamNum is 2 or 3))
            Announce(player, $"{TeamName(team)} {(choice == "stay" ? "stay" : "switch")}", $"Playing {TeamName(sideNow)}. Good luck.");
    }

    /// <summary>Ends the vote without a result (MatchZy already went live, or the vote finished).</summary>
    private void KnifeStop()
    {
        knifeTimer?.Kill();
        knifeTimer = null;
        if (knife is not null)
        {
            foreach (var slot in knifeVotes.Keys)
            {
                var player = Utilities.GetPlayerFromSlot(slot);
                if (player is not { IsValid: true } || !knife.IsOpenFor(player)) continue;
                knife.SetClassFor(player, "knife", "shown", false);
                var closing = slot;
                // let the panel slide out first
                AddTimer(0.6f, () =>
                {
                    var still = Utilities.GetPlayerFromSlot(closing);
                    if (still is { IsValid: true } && knife is not null && knife.IsOpenFor(still) && !knifeVotes.ContainsKey(closing)) knife.Close(still);
                });
            }
        }
        knifeVotes.Clear();
        foreach (var key in applied.Keys.Where(k => k.PanelId.StartsWith("knife")).ToList()) applied.Remove(key);
    }
}
