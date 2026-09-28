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
        player.PrintToCenter($"LEGACY-X\n5V5 · MATCH #{LegacyXDisplayMatchNumber()}");
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
        var overlay = $"LEGACY-X · MATCH #{LegacyXDisplayMatchNumber()} · LIVE";
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
            "team1" => $"{matchzyTeam1.teamName} WON",
            "team2" => $"{matchzyTeam2.teamName} WON",
            _ => "DRAW",
        };
        PrintToAllChat($"FINAL · {ChatColors.White}{outcome}{ChatColors.Grey} · RANK AND EXP SAVED");
    }
}
