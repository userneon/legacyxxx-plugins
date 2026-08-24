using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

public partial class MatchZy
{
    private CounterStrikeSharp.API.Modules.Timers.Timer? legacyXMatchOverlayTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? legacyXMatchOverlayStopTimer;
    private bool legacyXPostMatchSummarySent;

    private string LegacyXDisplayMatchNumber()
    {
        if (matchCoreId.HasValue) return matchCoreId.Value.ToString("N")[..8].ToUpperInvariant();
        return liveMatchId > 0 ? liveMatchId.ToString() : "PUG";
    }

    private void ShowLegacyXWelcome(CCSPlayerController player)
    {
        if (!legacyXQuietOverlayEnabled.Value || !IsPlayerValid(player) || player.IsBot || player.IsHLTV) return;
        player.PrintToCenter($"LEGACY-X\nFair 5v5 competitive\nMatch #{LegacyXDisplayMatchNumber()}");
    }

    private void StartLegacyXMatchOverlay()
    {
        if (!legacyXQuietOverlayEnabled.Value) return;
        legacyXPostMatchSummarySent = false;
        KillLegacyXMatchOverlay();
        ShowLegacyXMatchOverlay();
        legacyXMatchOverlayTimer = AddTimer(8.0f, ShowLegacyXMatchOverlay, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
        legacyXMatchOverlayStopTimer = AddTimer(legacyXMatchOverlayDuration.Value, KillLegacyXMatchOverlay);
    }

    private void ShowLegacyXMatchOverlay()
    {
        if (!isMatchLive)
        {
            KillLegacyXMatchOverlay();
            return;
        }
        var overlay = $"LEGACY-X  •  MATCH #{LegacyXDisplayMatchNumber()}\nLIVE  •  Fair play. Team integrity required to resume.";
        foreach (var player in Utilities.GetPlayers().Where(player => IsPlayerValid(player) && !player.IsBot && !player.IsHLTV))
        {
            player.PrintToCenter(overlay);
        }
    }

    private void KillLegacyXMatchOverlay()
    {
        legacyXMatchOverlayTimer?.Kill();
        legacyXMatchOverlayStopTimer?.Kill();
        legacyXMatchOverlayTimer = null;
        legacyXMatchOverlayStopTimer = null;
    }

    private void ShowLegacyXPostMatchSummary(string winnerTeam)
    {
        if (legacyXPostMatchSummarySent) return;
        legacyXPostMatchSummarySent = true;
        KillLegacyXMatchOverlay();
        var outcome = winnerTeam switch
        {
            "team1" => $"{matchzyTeam1.teamName} won",
            "team2" => $"{matchzyTeam2.teamName} won",
            _ => "Match drawn",
        };
        PrintToAllChat($"{ChatColors.Green}LEGACY-X final:{ChatColors.Default} {outcome}. Eligible original participants receive rank and XP once.");
    }
}
